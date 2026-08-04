using System.Text.Json;
using GreenBox.I18n.Cli;

namespace GreenBox.I18n.Cli.Tests;

public sealed class ValidateCommandTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        "GreenBox.I18n.Tests",
        Guid.NewGuid().ToString("N"));

    public ValidateCommandTests()
    {
        Directory.CreateDirectory(_temporaryDirectory);
    }

    [Fact]
    public void Execute_ValidCatalog_WritesValidJsonAndReturnsSuccess()
    {
        FileInfo catalogFile = WriteCatalog(
            """
            {
              "schemaVersion": 1,
              "entries": [
                {
                  "id": "1",
                  "path": "Reports.Title",
                  "locales": {
                    "en": {
                      "text": "Reports"
                    }
                  }
                }
              ]
            }
            """);
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();

        int exitCode = ValidateCommand.Execute(
            catalogFile,
            true,
            standardOutput,
            standardError);

        using JsonDocument report = JsonDocument.Parse(standardOutput.ToString());
        Assert.Equal(CliExitCodes.Success, exitCode);
        Assert.True(report.RootElement.GetProperty("isValid").GetBoolean());
        Assert.Equal(0, report.RootElement.GetProperty("errorCount").GetInt32());
        Assert.Empty(standardError.ToString());
    }

    [Fact]
    public void Execute_InvalidCatalog_WritesDiagnosticsAndReturnsInvalidData()
    {
        FileInfo catalogFile = WriteCatalog(
            """
            {
              "schemaVersion": 1,
              "entries": [
                {
                  "id": "0",
                  "path": "Reports..Title",
                  "locales": {}
                }
              ]
            }
            """);
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();

        int exitCode = ValidateCommand.Execute(
            catalogFile,
            true,
            standardOutput,
            standardError);

        using JsonDocument report = JsonDocument.Parse(standardOutput.ToString());
        JsonElement diagnostics = report.RootElement.GetProperty("diagnostics");
        Assert.Equal(CliExitCodes.InvalidData, exitCode);
        Assert.False(report.RootElement.GetProperty("isValid").GetBoolean());
        Assert.Equal("invalid_id", diagnostics[0].GetProperty("code").GetString());
        Assert.Equal("invalid_path", diagnostics[1].GetProperty("code").GetString());
        Assert.Empty(standardError.ToString());
    }

    [Fact]
    public void Execute_MalformedJson_WritesLineInformationAndReturnsInvalidData()
    {
        FileInfo catalogFile = WriteCatalog(
            """
            {
              "schemaVersion": 1,
              "entries": [
            }
            """);
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();

        int exitCode = ValidateCommand.Execute(
            catalogFile,
            true,
            standardOutput,
            standardError);

        using JsonDocument report = JsonDocument.Parse(standardOutput.ToString());
        JsonElement diagnostic = report.RootElement.GetProperty("diagnostics")[0];
        Assert.Equal(CliExitCodes.InvalidData, exitCode);
        Assert.Equal("invalid_json", diagnostic.GetProperty("code").GetString());
        Assert.Equal(4, diagnostic.GetProperty("line").GetInt32());
        Assert.Equal(0, diagnostic.GetProperty("position").GetInt32());
        Assert.Empty(standardError.ToString());
    }

    [Fact]
    public void Execute_MissingFile_WritesJsonAndReturnsExecutionError()
    {
        var catalogFile = new FileInfo(Path.Combine(_temporaryDirectory, "missing.json"));
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();

        int exitCode = ValidateCommand.Execute(
            catalogFile,
            true,
            standardOutput,
            standardError);

        using JsonDocument report = JsonDocument.Parse(standardOutput.ToString());
        JsonElement diagnostic = report.RootElement.GetProperty("diagnostics")[0];
        Assert.Equal(CliExitCodes.ExecutionError, exitCode);
        Assert.Equal("file_not_found", diagnostic.GetProperty("code").GetString());
        Assert.Empty(standardError.ToString());
    }

    [Fact]
    public void Execute_HumanOutput_UsesSingularWarningLabel()
    {
        FileInfo catalogFile = WriteCatalog(
            """
            {
              "schemaVersion": 1,
              "entries": [
                {
                  "id": "1",
                  "path": "Reports.Title",
                  "locales": {}
                }
              ]
            }
            """);
        var standardOutput = new StringWriter();
        var standardError = new StringWriter();

        int exitCode = ValidateCommand.Execute(
            catalogFile,
            false,
            standardOutput,
            standardError);

        Assert.Equal(CliExitCodes.Success, exitCode);
        Assert.Contains("Valid: 0 errors, 1 warning.", standardOutput.ToString());
        Assert.Empty(standardError.ToString());
    }

    public void Dispose()
    {
        Directory.Delete(_temporaryDirectory, true);
    }

    private FileInfo WriteCatalog(string json)
    {
        string path = Path.Combine(_temporaryDirectory, "catalog.json");
        File.WriteAllText(path, json);
        return new FileInfo(path);
    }
}
