using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GreenBox.I18n.Workspace.Contracts;

namespace GreenBox.I18n.Workspace;

public sealed partial class CatalogWorkspace
{
    private static readonly TimeSpan ChangeSetLifetime = TimeSpan.FromMinutes(15);

    public WorkspaceChangeSetResponse PrepareEntryChanges(PrepareEntryChangesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Changes);

        lock (_lock)
        {
            EnsureCatalogOpen();
            EnsureRevision(request.ExpectedRevision);
            PruneChangeSets();

            I18nCatalog candidate = CloneCatalog(_catalog!);
            var previews = new List<WorkspaceEntryChangePreview>();
            var warnings = new List<WorkspaceChangeNotice>();
            var blockers = CreateWorkspaceBlockers();
            var touchedIds = new HashSet<string>(StringComparer.Ordinal);
            int created = 0;
            int updated = 0;
            int deleted = 0;

            foreach (WorkspaceEntryMutation mutation in request.Changes)
            {
                string operation = mutation.Operation?.Trim().ToLowerInvariant() ?? string.Empty;
                if (operation is not ("create" or "update" or "delete"))
                {
                    throw new WorkspaceException(
                        WorkspaceErrorCodes.InvalidRequest,
                        $"Unsupported entry operation '{mutation.Operation}'. Use create, update, or delete.");
                }

                if (operation == "create")
                {
                    I18nEntry entry = CreateCandidateEntry(candidate, mutation);
                    if (!touchedIds.Add(entry.Id))
                    {
                        throw DuplicateMutation(entry.Id);
                    }

                    ApplyLocalePatches(candidate, entry, mutation.Locales);
                    previews.Add(new WorkspaceEntryChangePreview("create", entry.Id, null, entry.Path));
                    created++;
                    continue;
                }

                long id = ParseRequiredId(mutation.Id);
                string idText = id.ToString(CultureInfo.InvariantCulture);
                if (!touchedIds.Add(idText))
                {
                    throw DuplicateMutation(idText);
                }

                I18nEntry entryToChange = candidate.FindById(id) ?? throw new WorkspaceException(
                    I18nEditCodes.EntryNotFound,
                    $"Entry with ID {idText} was not found.");
                string beforePath = entryToChange.Path;
                if (operation == "delete")
                {
                    AddDeletionSafety(idText, request, warnings, blockers);
                    I18nEditResult removal = candidate.RemoveEntry(id);
                    ThrowIfFailed(removal);
                    previews.Add(new WorkspaceEntryChangePreview("delete", idText, beforePath, null));
                    deleted++;
                    continue;
                }

                if (mutation.Path != null)
                {
                    ThrowIfFailed(candidate.MoveEntry(id, mutation.Path));
                    entryToChange = candidate.FindById(id)!;
                }

                if (mutation.SetComment)
                {
                    ThrowIfFailed(candidate.SetEntryComment(id, mutation.Comment));
                }

                ApplyLocalePatches(candidate, entryToChange, mutation.Locales);
                previews.Add(new WorkspaceEntryChangePreview("update", idText, beforePath, entryToChange.Path));
                updated++;
            }

            return StoreChangeSet(
                candidate,
                new WorkspaceChangeSummaryResponse(created, updated, deleted, 0),
                previews,
                warnings,
                blockers,
                request.UsageRevision);
        }
    }

    public WorkspaceChangeSetResponse PrepareLocaleChanges(PrepareLocaleChangesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_lock)
        {
            EnsureCatalogOpen();
            EnsureRevision(request.ExpectedRevision);
            PruneChangeSets();

            I18nCatalog candidate = CloneCatalog(_catalog!);
            foreach (CatalogLocaleRenameRequest rename in request.Renames)
            {
                CatalogEditResult? error = RenameLocaleReferences(candidate, rename);
                if (error?.Error != null)
                {
                    throw new WorkspaceException(error.Error.Code, error.Error.Message);
                }
            }

            foreach (string removedId in request.RemovedIds.Distinct(StringComparer.Ordinal))
            {
                CatalogEditResult? error = RemoveLocaleReferences(candidate, removedId);
                if (error?.Error != null)
                {
                    throw new WorkspaceException(error.Error.Code, error.Error.Message);
                }
            }

            candidate.DefaultLocale = request.DefaultLocale;
            candidate.Locales = request.Locales.Select(CreateLocale).ToList();
            return StoreChangeSet(
                candidate,
                new WorkspaceChangeSummaryResponse(0, 0, 0, CountChangedLocales(_catalog!, candidate)),
                Array.Empty<WorkspaceEntryChangePreview>(),
                Array.Empty<WorkspaceChangeNotice>(),
                CreateWorkspaceBlockers(),
                null);
        }
    }

    public ApplyWorkspaceChangeSetResponse ApplyChangeSet(
        string changeSetId,
        string? currentUsageRevision = null)
    {
        if (string.IsNullOrWhiteSpace(changeSetId))
        {
            throw new WorkspaceException(WorkspaceErrorCodes.InvalidRequest, "A change-set ID is required.");
        }

        lock (_lock)
        {
            EnsureCatalogOpen();
            PruneChangeSets();
            if (!_changeSets.TryGetValue(changeSetId, out PreparedWorkspaceChangeSet? prepared))
            {
                throw new WorkspaceException(
                    WorkspaceErrorCodes.ChangeSetNotFound,
                    "The change set does not exist or has expired. Prepare the changes again.");
            }

            if (prepared.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            {
                _changeSets.Remove(changeSetId);
                throw new WorkspaceException(
                    WorkspaceErrorCodes.ChangeSetExpired,
                    "The change set expired. Prepare the changes again.");
            }

            EnsureRevision(prepared.BaseRevision);
            if (prepared.UsageRevision != null &&
                !string.Equals(prepared.UsageRevision, currentUsageRevision, StringComparison.Ordinal))
            {
                throw new WorkspaceException(
                    WorkspaceErrorCodes.UsageIndexChanged,
                    "The Unity usage index changed after deletion was prepared. Prepare the changes again.");
            }

            if (prepared.Response.Blockers.Count > 0)
            {
                throw new WorkspaceException(
                    WorkspaceErrorCodes.ChangeSetBlocked,
                    "The change set has blockers and cannot be applied.");
            }

            if (HasChangesUnsafe())
            {
                throw new WorkspaceException(
                    WorkspaceErrorCodes.WorkspaceHasUnsavedChanges,
                    "The Web editor has unsaved changes. Save or revert them before applying MCP changes.");
            }

            CatalogSourceStatusResponse sourceStatus = GetSourceStatusUnsafe();
            if (!sourceStatus.IsAvailable || sourceStatus.HasChanged)
            {
                throw new WorkspaceException(
                    WorkspaceErrorCodes.CatalogChangedExternally,
                    "The catalog changed on disk after the change set was prepared. Reopen it and prepare the changes again.");
            }

            I18nValidationResult validation = I18nCatalogValidator.Validate(prepared.Catalog);
            if (validation.HasErrors)
            {
                throw new WorkspaceException(
                    WorkspaceErrorCodes.InvalidCatalog,
                    "The prepared catalog no longer passes validation.");
            }

            byte[] savedBytes = new UTF8Encoding(false).GetBytes(I18nCatalogJson.Serialize(prepared.Catalog));
            try
            {
                WriteAtomically(_catalogPath!, savedBytes);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new WorkspaceException(WorkspaceErrorCodes.CatalogWriteFailed, exception.Message);
            }

            _catalog = CloneCatalog(prepared.Catalog);
            _baselineCatalog = CloneCatalog(prepared.Catalog);
            _baselineHash = Convert.ToHexString(SHA256.HashData(savedBytes));
            ClearDirtyState();
            _revision++;
            _changeSets.Clear();
            return new ApplyWorkspaceChangeSetResponse(changeSetId, _revision, _catalogPath!, true);
        }
    }

    private WorkspaceChangeSetResponse StoreChangeSet(
        I18nCatalog candidate,
        WorkspaceChangeSummaryResponse summary,
        IReadOnlyList<WorkspaceEntryChangePreview> previews,
        IReadOnlyList<WorkspaceChangeNotice> warnings,
        List<WorkspaceChangeNotice> blockers,
        string? usageRevision)
    {
        I18nValidationResult validation = I18nCatalogValidator.Validate(candidate);
        CatalogDiagnosticResponse[] diagnostics = validation.Diagnostics
            .Select(CreateDiagnosticResponse)
            .ToArray();
        if (validation.HasErrors)
        {
            blockers.Add(new WorkspaceChangeNotice(
                WorkspaceErrorCodes.InvalidCatalog,
                $"The proposed changes produce {validation.ErrorCount.ToString(CultureInfo.InvariantCulture)} catalog validation error(s)."));
        }

        string id = Guid.NewGuid().ToString("N");
        DateTimeOffset expiresAtUtc = DateTimeOffset.UtcNow.Add(ChangeSetLifetime);
        var response = new WorkspaceChangeSetResponse(
            id,
            _revision,
            expiresAtUtc,
            blockers.Count == 0,
            summary,
            previews,
            warnings,
            blockers,
            diagnostics);
        _changeSets.Add(id, new PreparedWorkspaceChangeSet(
            _revision,
            expiresAtUtc,
            CloneCatalog(candidate),
            response,
            usageRevision));
        return response;
    }

    private List<WorkspaceChangeNotice> CreateWorkspaceBlockers()
    {
        var blockers = new List<WorkspaceChangeNotice>();
        if (HasChangesUnsafe())
        {
            blockers.Add(new WorkspaceChangeNotice(
                WorkspaceErrorCodes.WorkspaceHasUnsavedChanges,
                "The Web editor has unsaved changes. Save or revert them before preparing MCP writes."));
        }

        CatalogSourceStatusResponse sourceStatus = GetSourceStatusUnsafe();
        if (!sourceStatus.IsAvailable || sourceStatus.HasChanged)
        {
            blockers.Add(new WorkspaceChangeNotice(
                WorkspaceErrorCodes.CatalogChangedExternally,
                "The source catalog is unavailable or changed externally. Reopen or merge it first."));
        }

        return blockers;
    }

    private static I18nEntry CreateCandidateEntry(I18nCatalog candidate, WorkspaceEntryMutation mutation)
    {
        if (string.IsNullOrWhiteSpace(mutation.Path))
        {
            throw new WorkspaceException(WorkspaceErrorCodes.InvalidRequest, "A path is required when creating an entry.");
        }

        if (string.IsNullOrWhiteSpace(mutation.Id))
        {
            I18nEditResult result = candidate.AddEntry(mutation.Path);
            ThrowIfFailed(result);
            I18nEntry allocated = result.Entry!;
            if (mutation.SetComment)
            {
                allocated.Comment = mutation.Comment;
            }

            return allocated;
        }

        long id = ParseRequiredId(mutation.Id);
        if (candidate.FindById(id) != null)
        {
            throw new WorkspaceException(I18nValidationCodes.DuplicateId, $"Entry with ID {mutation.Id} already exists.");
        }

        var entry = new I18nEntry
        {
            Id = mutation.Id,
            Path = mutation.Path,
            Comment = mutation.SetComment ? mutation.Comment : null,
            Locales = new Dictionary<string, I18nLocaleValue>(StringComparer.Ordinal),
        };
        I18nBatchEditResult addResult = candidate.ApplyEntryDelta(new[] { entry }, Array.Empty<long>());
        ThrowIfFailed(addResult);
        return candidate.FindById(id)!;
    }

    private static void ApplyLocalePatches(
        I18nCatalog candidate,
        I18nEntry entry,
        IReadOnlyDictionary<string, WorkspaceLocaleValuePatch>? patches)
    {
        if (patches == null)
        {
            return;
        }

        long id = ParseRequiredId(entry.Id);
        foreach ((string localeId, WorkspaceLocaleValuePatch patch) in patches)
        {
            if (patch.SetText)
            {
                ThrowIfFailed(candidate.SetEntryText(id, localeId, patch.Text));
            }

            if (patch.SetAsset)
            {
                I18nAssetReference? asset = patch.Asset == null
                    ? null
                    : new I18nAssetReference
                    {
                        AssetGuid = patch.Asset.AssetGuid,
                        LocalFileId = patch.Asset.LocalFileId,
                    };
                ThrowIfFailed(candidate.SetEntryAsset(id, localeId, asset));
            }
        }
    }

    private static void AddDeletionSafety(
        string id,
        PrepareEntryChangesRequest request,
        List<WorkspaceChangeNotice> warnings,
        List<WorkspaceChangeNotice> blockers)
    {
        if (request.UsageCounts == null || !request.UsageCounts.TryGetValue(id, out int? count) || count == null)
        {
            blockers.Add(new WorkspaceChangeNotice(
                "usage_unknown",
                "Usage information is unavailable, so this entry cannot be deleted safely.",
                id));
            return;
        }

        if (count <= 0)
        {
            return;
        }

        var notice = new WorkspaceChangeNotice(
            "entry_has_usages",
            $"Entry {id} has {count.Value.ToString(CultureInfo.InvariantCulture)} indexed usage(s).",
            id);
        if (request.AllowDeleteWithUsages)
        {
            warnings.Add(notice);
        }
        else
        {
            blockers.Add(notice);
        }
    }

    private void EnsureRevision(long expectedRevision)
    {
        if (expectedRevision != _revision)
        {
            throw new WorkspaceException(
                WorkspaceErrorCodes.CatalogRevisionMismatch,
                $"The operation expected revision {expectedRevision}, but the workspace is at revision {_revision}.");
        }
    }

    private static long ParseRequiredId(string? id)
    {
        if (!I18nEntryId.TryParse(id, out long numericId))
        {
            throw new WorkspaceException(
                I18nEditCodes.InvalidId,
                $"Entry ID does not use the GreenBox I18n ID format: '{id}'.");
        }

        return numericId;
    }

    private static WorkspaceException DuplicateMutation(string id) => new(
        WorkspaceErrorCodes.InvalidRequest,
        $"Entry {id} occurs more than once in the same change set.");

    private static void ThrowIfFailed(I18nEditResult result)
    {
        if (!result.IsSuccess)
        {
            throw new WorkspaceException(result.Error!.Code, result.Error.Message);
        }
    }

    private static void ThrowIfFailed(I18nBatchEditResult result)
    {
        if (!result.IsSuccess)
        {
            throw new WorkspaceException(result.Error!.Code, result.Error.Message);
        }
    }

    private static int CountChangedLocales(I18nCatalog before, I18nCatalog after)
    {
        var ids = new HashSet<string>(before.Locales.Select(locale => locale.Id), StringComparer.Ordinal);
        ids.UnionWith(after.Locales.Select(locale => locale.Id));
        return ids.Count(id =>
        {
            I18nLocaleDefinition? left = before.Locales.FirstOrDefault(locale => locale.Id == id);
            I18nLocaleDefinition? right = after.Locales.FirstOrDefault(locale => locale.Id == id);
            return !LocaleEquals(left, right);
        });
    }

    private void PruneChangeSets()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (string id in _changeSets
                     .Where(pair => pair.Value.ExpiresAtUtc <= now)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _changeSets.Remove(id);
        }

        if (_changeSets.Count <= 128)
        {
            return;
        }

        foreach (string id in _changeSets
                     .OrderBy(pair => pair.Value.ExpiresAtUtc)
                     .Take(_changeSets.Count - 128)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            _changeSets.Remove(id);
        }
    }

    private sealed record PreparedWorkspaceChangeSet(
        long BaseRevision,
        DateTimeOffset ExpiresAtUtc,
        I18nCatalog Catalog,
        WorkspaceChangeSetResponse Response,
        string? UsageRevision);
}
