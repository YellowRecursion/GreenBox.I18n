using System.Text.Json;
using GreenBox.I18n;
using GreenBox.I18n.Cli;

namespace GreenBox.I18n.Cli.Tests;

public sealed class GitInstallCommandTests : IDisposable
{
    private readonly string _repository = Path.Combine(
        Path.GetTempPath(),
        "GreenBox.I18n.GitTests",
        Guid.NewGuid().ToString("N"));

    public GitInstallCommandTests()
    {
        Directory.CreateDirectory(_repository);
        GitProcessResult result = GitProcess.Run("-C", _repository, "init", "--quiet");
        Assert.True(result.IsSuccess, result.StandardError);
        Assert.True(RunGit("config", "user.email", "tests@greenbox.local").IsSuccess);
        Assert.True(RunGit("config", "user.name", "GreenBox Tests").IsSuccess);
    }

    [Fact]
    public void Execute_ConfiguresRequestedGitScope()
    {
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();

        int exitCode = GitInstallCommand.Execute(
            "test-i18n",
            GitConfigurationTarget.Repository(_repository),
            true,
            standardOutput,
            standardError);

        GitProcessResult driver = GitProcess.Run(
            "-C", _repository, "config", "--local", "--get", "merge.greenbox-i18n.driver");
        using JsonDocument report = JsonDocument.Parse(standardOutput.ToString());
        Assert.Equal(CliExitCodes.Success, exitCode);
        Assert.True(driver.IsSuccess, driver.StandardError);
        Assert.Equal(
            "test-i18n merge --base \"%O\" --current \"%A\" --incoming \"%B\" --output \"%A\"",
            driver.StandardOutput);
        Assert.True(report.RootElement.GetProperty("configured").GetBoolean());
        Assert.Equal("repository", report.RootElement.GetProperty("scope").GetString());
        Assert.Empty(standardError.ToString());
    }

    [Fact]
    public void Execute_RepeatedInstallation_IsIdempotent()
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            int exitCode = GitInstallCommand.Execute(
                "test-i18n",
                GitConfigurationTarget.Repository(_repository),
                false,
                new StringWriter(),
                new StringWriter());
            Assert.Equal(CliExitCodes.Success, exitCode);
        }

        GitProcessResult values = RunGit(
            "config", "--local", "--get-all", "merge.greenbox-i18n.driver");
        Assert.True(values.IsSuccess, values.StandardError);
        Assert.Single(values.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void InstalledDriver_GitMergeCombinesIndependentCatalogChanges()
    {
        FileInfo catalog = CreateCatalog();
        string executable = Path.Combine(
            AppContext.BaseDirectory,
            OperatingSystem.IsWindows() ? "i18n.exe" : "i18n");
        Assert.True(File.Exists(executable), $"CLI app host was not found: {executable}");
        Assert.Equal(
            CliExitCodes.Success,
            GitInstallCommand.Execute(
                executable,
                GitConfigurationTarget.Repository(_repository),
                false,
                new StringWriter(),
                new StringWriter()));
        File.WriteAllText(
            Path.Combine(catalog.DirectoryName!, ".gitattributes"),
            "/localization.json text eol=lf merge=greenbox-i18n\n");

        Assert.True(RunGit("add", ".").IsSuccess);
        Assert.True(RunGit("commit", "--quiet", "-m", "base").IsSuccess);
        string baseBranch = RunGit("branch", "--show-current").StandardOutput;
        Assert.True(RunGit("checkout", "--quiet", "-b", "incoming").IsSuccess);
        UpdateCatalog(catalog, value => value.Entries[1].Locales["en"].Text = "Incoming reports");
        Assert.True(RunGit("add", catalog.FullName).IsSuccess);
        Assert.True(RunGit("commit", "--quiet", "-m", "incoming").IsSuccess);

        Assert.True(RunGit("checkout", "--quiet", baseBranch).IsSuccess);
        UpdateCatalog(catalog, value => value.Entries[0].Comment = "Local comment");
        Assert.True(RunGit("add", catalog.FullName).IsSuccess);
        Assert.True(RunGit("commit", "--quiet", "-m", "current").IsSuccess);

        GitProcessResult merge = RunGit("merge", "--no-edit", "incoming");

        Assert.True(merge.IsSuccess, merge.StandardError);
        I18nCatalog merged = I18nCatalogJson.Deserialize(File.ReadAllText(catalog.FullName));
        Assert.Equal("Local comment", merged.Entries[0].Comment);
        Assert.Equal("Incoming reports", merged.Entries[1].Locales["en"].Text);
    }

    public void Dispose()
    {
        foreach (string file in Directory.EnumerateFiles(_repository, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_repository, true);
    }

    private FileInfo CreateCatalog()
    {
        string directory = Path.Combine(_repository, "Assets", "GreenBox.I18n");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "localization.json");
        File.WriteAllText(path, TestCatalog.ValidJson);
        return new FileInfo(path);
    }

    private GitProcessResult RunGit(params string[] arguments)
    {
        return GitProcess.Run(new[] { "-C", _repository }.Concat(arguments).ToArray());
    }

    private static void UpdateCatalog(FileInfo catalog, Action<I18nCatalog> update)
    {
        I18nCatalog value = I18nCatalogJson.Deserialize(File.ReadAllText(catalog.FullName));
        update(value);
        File.WriteAllText(catalog.FullName, I18nCatalogJson.Serialize(value));
    }
}
