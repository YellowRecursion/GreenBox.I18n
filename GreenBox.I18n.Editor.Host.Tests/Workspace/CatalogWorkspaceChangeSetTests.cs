using System.Security.Cryptography;
using System.Text;
using GreenBox.I18n.Workspace;
using GreenBox.I18n.Workspace.Contracts;

namespace GreenBox.I18n.Editor.Host.Tests.Workspace;

public sealed class CatalogWorkspaceChangeSetTests
{
    [Fact]
    public void ApplyChangeSet_ValidBatch_SavesAtomicallyAndAdvancesRevision()
    {
        using TemporaryWorkspace temporary = TemporaryWorkspace.Create();
        CatalogWorkspace workspace = temporary.Workspace;
        long revision = workspace.GetSnapshot().Revision;

        WorkspaceChangeSetResponse prepared = workspace.PrepareEntryChanges(
            new PrepareEntryChangesRequest(
                revision,
                new[]
                {
                    new WorkspaceEntryMutation(
                        "update",
                        EntryId,
                        SetComment: true,
                        Comment: "Changed by MCP",
                        Locales: new Dictionary<string, WorkspaceLocaleValuePatch>
                        {
                            ["en"] = new WorkspaceLocaleValuePatch(SetText: true, Text: "Start"),
                        }),
                }));

        Assert.True(prepared.CanApply);
        ApplyWorkspaceChangeSetResponse applied = workspace.ApplyChangeSet(prepared.ChangeSetId);

        Assert.True(applied.Saved);
        Assert.Equal(revision + 1, applied.Revision);
        I18nCatalog saved = I18nCatalogJson.Deserialize(File.ReadAllText(temporary.CatalogPath));
        I18nEntry entry = Assert.Single(saved.Entries);
        Assert.Equal("Changed by MCP", entry.Comment);
        Assert.Equal("Start", entry.Locales["en"].Text);
        Assert.False(workspace.GetCatalogSnapshot()!.HasChanges);
    }

    [Fact]
    public void ApplyChangeSet_WebEditAfterPreparation_IsRejected()
    {
        using TemporaryWorkspace temporary = TemporaryWorkspace.Create();
        CatalogWorkspace workspace = temporary.Workspace;
        WorkspaceChangeSetResponse prepared = PrepareCommentChange(workspace);
        workspace.AddEntry("Menu.Settings");

        WorkspaceException exception = Assert.Throws<WorkspaceException>(
            () => workspace.ApplyChangeSet(prepared.ChangeSetId));

        Assert.Equal(WorkspaceErrorCodes.CatalogRevisionMismatch, exception.Code);
    }

    [Fact]
    public void ApplyChangeSet_SourceEditAfterPreparation_IsRejected()
    {
        using TemporaryWorkspace temporary = TemporaryWorkspace.Create();
        CatalogWorkspace workspace = temporary.Workspace;
        WorkspaceChangeSetResponse prepared = PrepareCommentChange(workspace);
        File.AppendAllText(temporary.CatalogPath, Environment.NewLine);

        WorkspaceException exception = Assert.Throws<WorkspaceException>(
            () => workspace.ApplyChangeSet(prepared.ChangeSetId));

        Assert.Equal(WorkspaceErrorCodes.CatalogChangedExternally, exception.Code);
    }

    [Fact]
    public void PrepareDelete_UnknownUsage_IsBlocked()
    {
        using TemporaryWorkspace temporary = TemporaryWorkspace.Create();
        CatalogWorkspace workspace = temporary.Workspace;

        WorkspaceChangeSetResponse prepared = workspace.PrepareEntryChanges(
            new PrepareEntryChangesRequest(
                workspace.GetSnapshot().Revision,
                new[] { new WorkspaceEntryMutation("delete", EntryId) }));

        Assert.False(prepared.CanApply);
        WorkspaceChangeNotice blocker = Assert.Single(prepared.Blockers, item => item.Code == "usage_unknown");
        Assert.Equal(EntryId, blocker.EntryId);
    }

    [Fact]
    public void ApplyPreparedDelete_UsageIndexChanged_IsRejected()
    {
        using TemporaryWorkspace temporary = TemporaryWorkspace.Create();
        CatalogWorkspace workspace = temporary.Workspace;
        WorkspaceChangeSetResponse prepared = workspace.PrepareEntryChanges(
            new PrepareEntryChangesRequest(
                workspace.GetSnapshot().Revision,
                new[] { new WorkspaceEntryMutation("delete", EntryId) },
                new Dictionary<string, int?> { [EntryId] = 0 },
                UsageRevision: "usage-v1"));
        Assert.True(prepared.CanApply);

        WorkspaceException exception = Assert.Throws<WorkspaceException>(
            () => workspace.ApplyChangeSet(prepared.ChangeSetId, "usage-v2"));

        Assert.Equal(WorkspaceErrorCodes.UsageIndexChanged, exception.Code);
    }

    [Fact]
    public void PrepareCreate_WithKnownMissingId_PreservesThatId()
    {
        using TemporaryWorkspace temporary = TemporaryWorkspace.Create();
        CatalogWorkspace workspace = temporary.Workspace;
        const string missingId = "3857381007564118867";

        WorkspaceChangeSetResponse prepared = workspace.PrepareEntryChanges(
            new PrepareEntryChangesRequest(
                workspace.GetSnapshot().Revision,
                new[]
                {
                    new WorkspaceEntryMutation(
                        "create",
                        missingId,
                        "Recovered.Entry",
                        Locales: new Dictionary<string, WorkspaceLocaleValuePatch>
                        {
                            ["en"] = new WorkspaceLocaleValuePatch(SetText: true, Text: "Recovered"),
                        }),
                }));
        Assert.True(prepared.CanApply);

        workspace.ApplyChangeSet(prepared.ChangeSetId);

        CatalogEntryResponse restored = Assert.Single(
            workspace.GetCatalogSnapshot()!.Entries,
            entry => entry.Id == missingId);
        Assert.Equal("Recovered.Entry", restored.Path);
    }

    [Fact]
    public void PrepareLocaleChanges_AddLocale_AppliesAsOneSavedBatch()
    {
        using TemporaryWorkspace temporary = TemporaryWorkspace.Create();
        CatalogWorkspace workspace = temporary.Workspace;

        WorkspaceChangeSetResponse prepared = workspace.PrepareLocaleChanges(
            new PrepareLocaleChangesRequest(
                workspace.GetSnapshot().Revision,
                "en",
                new[]
                {
                    new CatalogLocaleEditRequest("en", "English", "en-US", null, null),
                    new CatalogLocaleEditRequest("ru", "Русский", "ru-RU", "en", null),
                },
                Array.Empty<CatalogLocaleRenameRequest>(),
                Array.Empty<string>()));

        Assert.True(prepared.CanApply);
        Assert.Equal(1, prepared.Summary.ChangedLocales);
        workspace.ApplyChangeSet(prepared.ChangeSetId);
        Assert.Contains(workspace.GetCatalogSnapshot()!.Locales, locale => locale.Id == "ru");
    }

    [Fact]
    public void SearchCursor_AfterRevisionChange_IsRejected()
    {
        using TemporaryWorkspace temporary = TemporaryWorkspace.Create();
        CatalogWorkspace workspace = temporary.Workspace;
        CatalogEditResult added = workspace.AddEntry("Menu.Settings");
        Assert.Null(added.Error);
        WorkspaceEntrySearchResponse first = workspace.SearchEntries(
            new WorkspaceEntrySearchRequest(Limit: 1));
        Assert.NotNull(first.NextCursor);
        byte[] sourceBytes = File.ReadAllBytes(temporary.CatalogPath);
        workspace.Open(
            temporary.CatalogPath,
            I18nCatalogJson.Deserialize(Encoding.UTF8.GetString(sourceBytes)),
            Convert.ToHexString(SHA256.HashData(sourceBytes)));

        WorkspaceException exception = Assert.Throws<WorkspaceException>(() =>
            workspace.SearchEntries(new WorkspaceEntrySearchRequest(Cursor: first.NextCursor, Limit: 1)));

        Assert.Equal(WorkspaceErrorCodes.CatalogRevisionMismatch, exception.Code);
    }

    private static WorkspaceChangeSetResponse PrepareCommentChange(CatalogWorkspace workspace) =>
        workspace.PrepareEntryChanges(new PrepareEntryChangesRequest(
            workspace.GetSnapshot().Revision,
            new[]
            {
                new WorkspaceEntryMutation(
                    "update",
                    EntryId,
                    SetComment: true,
                    Comment: "Prepared"),
            }));

    private const string EntryId = "3857333080842834967";

    private sealed class TemporaryWorkspace : IDisposable
    {
        private TemporaryWorkspace(string directory, string catalogPath, CatalogWorkspace workspace)
        {
            Directory = directory;
            CatalogPath = catalogPath;
            Workspace = workspace;
        }

        public string Directory { get; }
        public string CatalogPath { get; }
        public CatalogWorkspace Workspace { get; }

        public static TemporaryWorkspace Create()
        {
            string directory = Path.Combine(Path.GetTempPath(), $"greenbox-workspace-{Guid.NewGuid():N}");
            System.IO.Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "localization.json");
            I18nCatalog catalog = CreateCatalog();
            byte[] bytes = new UTF8Encoding(false).GetBytes(I18nCatalogJson.Serialize(catalog));
            File.WriteAllBytes(path, bytes);
            var workspace = new CatalogWorkspace();
            workspace.Open(path, catalog, Convert.ToHexString(SHA256.HashData(bytes)));
            return new TemporaryWorkspace(directory, path, workspace);
        }

        public void Dispose()
        {
            System.IO.Directory.Delete(Directory, true);
        }

        private static I18nCatalog CreateCatalog()
        {
            var catalog = new I18nCatalog
            {
                DefaultLocale = "en",
                Locales =
                [
                    new I18nLocaleDefinition { Id = "en", DisplayName = "English", Culture = "en-US" },
                ],
                Entries =
                [
                    new I18nEntry
                    {
                        Id = EntryId,
                        Path = "Menu.Play",
                        Locales = new Dictionary<string, I18nLocaleValue>
                        {
                            ["en"] = new I18nLocaleValue { Text = "Play" },
                        },
                    },
                ],
            };
            return catalog;
        }
    }
}
