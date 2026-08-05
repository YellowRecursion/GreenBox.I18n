using GreenBox.I18n.Editor.Host.Editor;
using GreenBox.I18n.Editor.Host.Infrastructure;

namespace GreenBox.I18n.Editor.Host.Tests.Infrastructure;

public sealed class UnityAssetReferenceServiceTests
{
    private const string AssetGuid = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void Resolve_ReadsCurrentPathFromMetaFile()
    {
        using var project = new TestUnityProject(AssetGuid);
        var service = new UnityAssetReferenceService();

        UnityAssetReferenceResult result = service.Resolve(project.CatalogPath, AssetGuid);

        Assert.Null(result.Error);
        Assert.Equal("Assets/Audio/Victory.mp3", result.Reference?.AssetPath);
        Assert.Equal("Victory.mp3", result.Reference?.FileName);
    }

    [Fact]
    public void ResolveDrop_ReturnsExactUnitySubAssetSelection()
    {
        using var project = new TestUnityProject(AssetGuid);
        project.WriteSelection("21300002", "VictorySprite");
        var service = new UnityAssetReferenceService();

        UnityAssetReferenceResult result = service.ResolveDrop(project.CatalogPath, "Victory.mp3");

        Assert.Null(result.Error);
        Assert.Equal(AssetGuid, result.Reference?.AssetGuid);
        Assert.Equal("21300002", result.Reference?.LocalFileId);
        Assert.Equal("VictorySprite", result.Reference?.ObjectName);
    }

    [Fact]
    public void ResolveDrop_RejectsFileThatDoesNotMatchUnitySelection()
    {
        using var project = new TestUnityProject(AssetGuid);
        project.WriteSelection("21300002", "VictorySprite");
        var service = new UnityAssetReferenceService();

        UnityAssetReferenceResult result = service.ResolveDrop(project.CatalogPath, "Defeat.mp3");

        Assert.Equal(EditorErrorCodes.UnitySelectionMismatch, result.Error?.Code);
        Assert.Null(result.Reference);
    }

    private sealed class TestUnityProject : IDisposable
    {
        private readonly string _projectRoot;
        private readonly string _assetGuid;

        public TestUnityProject(string assetGuid)
        {
            _assetGuid = assetGuid;
            _projectRoot = Path.Combine(
                Path.GetTempPath(),
                "GreenBox.I18n.Tests",
                Guid.NewGuid().ToString("N"));
            string assetDirectory = Path.Combine(_projectRoot, "Assets", "Audio");
            Directory.CreateDirectory(assetDirectory);
            File.WriteAllText(Path.Combine(assetDirectory, "Victory.mp3"), "test");
            File.WriteAllText(
                Path.Combine(assetDirectory, "Victory.mp3.meta"),
                $"fileFormatVersion: 2{Environment.NewLine}guid: {_assetGuid}{Environment.NewLine}");
            CatalogPath = Path.Combine(_projectRoot, "Assets", "catalog.json");
            File.WriteAllText(CatalogPath, "{}");
        }

        public string CatalogPath { get; }

        public void WriteSelection(string localFileId, string objectName)
        {
            string directory = Path.Combine(_projectRoot, "Library", "GreenBox.I18n");
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, "active-selection.json"),
                $$"""
                {
                  "format": "greenbox.i18n.asset-reference",
                  "version": 1,
                  "assetGuid": "{{_assetGuid}}",
                  "localFileId": "{{localFileId}}",
                  "assetPath": "Assets/Audio/Victory.mp3",
                  "objectName": "{{objectName}}"
                }
                """);
        }

        public void Dispose()
        {
            Directory.Delete(_projectRoot, true);
        }
    }
}
