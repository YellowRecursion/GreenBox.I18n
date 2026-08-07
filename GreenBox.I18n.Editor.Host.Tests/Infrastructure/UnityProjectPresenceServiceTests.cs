using GreenBox.I18n.Editor.Host.Infrastructure;

namespace GreenBox.I18n.Editor.Host.Tests.Infrastructure;

public sealed class UnityProjectPresenceServiceTests
{
    [Fact]
    public void GetStatus_ReportsHeldUnityEditorLeaseAsOnline()
    {
        using var project = new TestUnityProject();
        using FileStream lease = project.HoldEditorLease();
        var service = new UnityProjectPresenceService(new UnityProjectLocator());

        var status = service.GetStatus(project.CatalogPath);

        Assert.True(status.IsUnityProject);
        Assert.Equal(project.ProjectName, status.ProjectName);
        Assert.Equal(project.ProjectPath, status.ProjectPath);
        Assert.True(status.IsEditorOnline);
    }

    [Fact]
    public void GetStatus_ReportsReleasedUnityEditorLeaseAsOffline()
    {
        using var project = new TestUnityProject();
        project.CreateReleasedEditorLease();
        var service = new UnityProjectPresenceService(new UnityProjectLocator());

        var status = service.GetStatus(project.CatalogPath);

        Assert.True(status.IsUnityProject);
        Assert.False(status.IsEditorOnline);
    }

    [Fact]
    public void GetStatus_DoesNotAssociateStandaloneCatalogWithUnity()
    {
        string directory = Path.Combine(Path.GetTempPath(), "GreenBox.I18n.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string catalogPath = Path.Combine(directory, "catalog.json");
        File.WriteAllText(catalogPath, "{}");
        try
        {
            var service = new UnityProjectPresenceService(new UnityProjectLocator());

            var status = service.GetStatus(catalogPath);

            Assert.False(status.IsUnityProject);
            Assert.Null(status.ProjectName);
            Assert.Null(status.ProjectPath);
            Assert.False(status.IsEditorOnline);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private sealed class TestUnityProject : IDisposable
    {
        public TestUnityProject()
        {
            ProjectPath = Path.Combine(
                Path.GetTempPath(),
                "GreenBox.I18n.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(ProjectPath, "Assets"));
            Directory.CreateDirectory(Path.Combine(ProjectPath, "ProjectSettings"));
            Directory.CreateDirectory(Path.GetDirectoryName(LeasePath)!);
            CatalogPath = Path.Combine(ProjectPath, "Assets", "catalog.json");
            File.WriteAllText(CatalogPath, "{}");
        }

        public string ProjectPath { get; }
        public string ProjectName => new DirectoryInfo(ProjectPath).Name;
        public string CatalogPath { get; }
        private string LeasePath => Path.Combine(ProjectPath, "Library", "GreenBox.I18n", "unity-editor.lock");

        public FileStream HoldEditorLease() =>
            new(LeasePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);

        public void CreateReleasedEditorLease()
        {
            using FileStream lease = HoldEditorLease();
        }

        public void Dispose()
        {
            Directory.Delete(ProjectPath, true);
        }
    }
}
