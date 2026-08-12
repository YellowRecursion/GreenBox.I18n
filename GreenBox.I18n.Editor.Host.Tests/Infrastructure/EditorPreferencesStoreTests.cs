using GreenBox.I18n.Editor.Host.Contracts;
using GreenBox.I18n.Editor.Host.Infrastructure;

namespace GreenBox.I18n.Editor.Host.Tests.Infrastructure;

public sealed class EditorPreferencesStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "GreenBox.I18n.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void NewStore_UsesHierarchyWarningDefaults()
    {
        var store = new EditorPreferencesStore(GetFilePath());

        EditorPreferencesResponse preferences = store.GetSnapshot();

        Assert.True(preferences.ReopenLastCatalog);
        Assert.True(preferences.WarnUnusedEntries);
        Assert.False(preferences.WarnIncompleteEntries);
    }

    [Fact]
    public void Update_PersistsHierarchyWarningPreferences()
    {
        string filePath = GetFilePath();
        var store = new EditorPreferencesStore(filePath);

        store.Update(new UpdateEditorPreferencesRequest(
            ReopenLastCatalog: false,
            WarnUnusedEntries: false,
            WarnIncompleteEntries: true));

        EditorPreferencesResponse restored = new EditorPreferencesStore(filePath).GetSnapshot();
        Assert.False(restored.ReopenLastCatalog);
        Assert.False(restored.WarnUnusedEntries);
        Assert.True(restored.WarnIncompleteEntries);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string GetFilePath()
    {
        return Path.Combine(_directory, "preferences.json");
    }
}
