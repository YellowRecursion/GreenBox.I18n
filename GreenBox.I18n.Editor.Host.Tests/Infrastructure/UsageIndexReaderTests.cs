using GreenBox.I18n.Editor.Host.Infrastructure;
using Microsoft.Data.Sqlite;

namespace GreenBox.I18n.Editor.Host.Tests.Infrastructure;

public sealed class UsageIndexReaderTests
{
    private const long EntryId = 3857353019861119080;

    [Fact]
    public async Task ReadSummaryAsync_ReturnsCurrentCountsAndIndexState()
    {
        using var project = new TestUsageProject();
        var reader = new UsageIndexReader(new UnityProjectLocator());

        var result = await reader.ReadSummaryAsync(project.CatalogPath, CancellationToken.None);

        Assert.Equal(UsageIndexAvailability.Available, result.Availability);
        Assert.Equal("ready", result.Status);
        Assert.Equal(1, result.FailedSourceCount);
        var entry = Assert.Single(result.Entries);
        Assert.Equal(EntryId.ToString(), entry.EntryId);
        Assert.Equal(2, entry.TotalCount);
        Assert.Equal(1, entry.CodeCount);
        Assert.Equal(1, entry.AssetCount);
    }

    [Fact]
    public async Task ReadStateAsync_DoesNotLoadPerEntryCounts()
    {
        using var project = new TestUsageProject();
        var reader = new UsageIndexReader(new UnityProjectLocator());

        var result = await reader.ReadStateAsync(project.CatalogPath, CancellationToken.None);

        Assert.Equal(UsageIndexAvailability.Available, result.Availability);
        Assert.Equal("ready", result.Status);
        Assert.NotNull(result.UpdatedAtUtc);
        Assert.Equal(1, result.FailedSourceCount);
    }

    [Fact]
    public async Task ReadEntryAsync_ReturnsCodeAndUnityObjectLocations()
    {
        using var project = new TestUsageProject();
        var reader = new UsageIndexReader(new UnityProjectLocator());

        var result = await reader.ReadEntryAsync(project.CatalogPath, EntryId, CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        var code = Assert.Single(result.Code);
        Assert.Equal("Assembly-CSharp", code.Assembly);
        Assert.Equal("Assets/Scripts/Shop.cs", code.FilePath);
        Assert.Equal(42, code.Line);

        var asset = Assert.Single(result.Assets);
        Assert.Equal("Assets/Scenes/Shop.unity", asset.AssetPath);
        Assert.Equal("11500000", asset.AssetLocalId);
        Assert.Equal("1001", asset.GameObjectLocalId);
        Assert.Equal("Canvas / Buy", asset.ObjectPath);
        Assert.Equal("I18nText", asset.ComponentType);
    }

    [Fact]
    public async Task ReadSummaryAsync_DoesNotTreatStandaloneCatalogAsZeroUsages()
    {
        string directory = Path.Combine(Path.GetTempPath(), "GreenBox.I18n.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string catalogPath = Path.Combine(directory, "catalog.json");
        File.WriteAllText(catalogPath, "{}");
        try
        {
            var reader = new UsageIndexReader(new UnityProjectLocator());

            var result = await reader.ReadSummaryAsync(catalogPath, CancellationToken.None);

            Assert.Equal(UsageIndexAvailability.OutsideUnityProject, result.Availability);
            Assert.Empty(result.Entries);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private sealed class TestUsageProject : IDisposable
    {
        private readonly string _projectRoot;

        public TestUsageProject()
        {
            _projectRoot = Path.Combine(
                Path.GetTempPath(),
                "GreenBox.I18n.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_projectRoot, "Assets"));
            Directory.CreateDirectory(Path.Combine(_projectRoot, "ProjectSettings"));
            string indexDirectory = Path.Combine(_projectRoot, "Library", "GreenBox.I18n");
            Directory.CreateDirectory(indexDirectory);
            CatalogPath = Path.Combine(_projectRoot, "Assets", "catalog.json");
            File.WriteAllText(CatalogPath, "{}");
            CreateIndex(Path.Combine(indexDirectory, "usage-index.db"));
        }

        public string CatalogPath { get; }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(_projectRoot, true);
        }

        private static void CreateIndex(string path)
        {
            using var connection = new SqliteConnection($"Data Source={path}");
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $$"""
                CREATE TABLE index_state (
                    id INTEGER PRIMARY KEY,
                    status TEXT NOT NULL,
                    updated_at_utc INTEGER,
                    last_error TEXT);
                CREATE TABLE sources (
                    source_id INTEGER PRIMARY KEY,
                    kind TEXT NOT NULL,
                    source_key TEXT NOT NULL,
                    asset_guid TEXT,
                    status TEXT NOT NULL,
                    error TEXT);
                CREATE TABLE code_usages (
                    source_id INTEGER NOT NULL,
                    entry_id INTEGER NOT NULL,
                    file_path TEXT NOT NULL,
                    line INTEGER NOT NULL);
                CREATE TABLE asset_usages (
                    usage_id INTEGER PRIMARY KEY,
                    source_id INTEGER NOT NULL,
                    entry_id INTEGER NOT NULL,
                    asset_local_id INTEGER NOT NULL,
                    game_object_local_id INTEGER,
                    object_path TEXT NOT NULL,
                    component_type TEXT NOT NULL,
                    property_path TEXT NOT NULL,
                    line INTEGER NOT NULL,
                    is_prefab_override INTEGER NOT NULL,
                    target_asset_guid TEXT,
                    target_local_id INTEGER);

                INSERT INTO index_state VALUES (1, 'ready', 1786021200000, NULL);
                INSERT INTO sources VALUES (1, 'assembly', 'Assembly-CSharp', NULL, 'current', NULL);
                INSERT INTO sources VALUES (2, 'asset', 'Assets/Scenes/Shop.unity',
                    '0123456789abcdef0123456789abcdef', 'current', NULL);
                INSERT INTO sources VALUES (3, 'asset', 'Assets/Scenes/Broken.unity', NULL, 'failed', 'Broken YAML');
                INSERT INTO sources VALUES (4, 'assembly', 'Old-Assembly', NULL, 'pending', NULL);
                INSERT INTO code_usages VALUES (1, {{EntryId}}, 'Assets/Scripts/Shop.cs', 42);
                INSERT INTO code_usages VALUES (4, {{EntryId}}, 'Assets/Scripts/Old.cs', 1);
                INSERT INTO asset_usages VALUES (
                    1, 2, {{EntryId}}, 11500000, 1001, 'Canvas / Buy', 'I18nText',
                    '_key._greenBoxI18nEntryId', 210, 0, NULL, NULL);
                """;
            command.ExecuteNonQuery();
        }
    }
}
