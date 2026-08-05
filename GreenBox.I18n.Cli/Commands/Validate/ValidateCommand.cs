using System.CommandLine;
using GreenBox.I18n;
using Newtonsoft.Json;

namespace GreenBox.I18n.Cli;

internal static class ValidateCommand
{
    public static Command Create()
    {
        var catalogArgument = new Argument<FileInfo>("catalog")
        {
            Description = "Path to the i18n catalog JSON file.",
        };

        var jsonOption = new Option<bool>("--json")
        {
            Description = "Write a machine-readable JSON report.",
        };

        var command = new Command("validate", "Validate an i18n catalog file.")
        {
            Arguments = { catalogArgument },
            Options = { jsonOption },
        };

        command.SetAction(parseResult => Execute(
            parseResult.GetValue(catalogArgument)!,
            parseResult.GetValue(jsonOption),
            Console.Out,
            Console.Error));

        return command;
    }

    internal static int Execute(
        FileInfo catalogFile,
        bool writeJson,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        if (!catalogFile.Exists)
        {
            ValidationReport report = CreateSingleErrorReport(
                catalogFile.FullName,
                CliDiagnosticCodes.FileNotFound,
                $"Catalog file was not found: {catalogFile.FullName}");
            WriteExecutionError(report, writeJson, standardOutput, standardError);
            return CliExitCodes.ExecutionError;
        }

        string json;
        try
        {
            json = File.ReadAllText(catalogFile.FullName);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ValidationReport report = CreateSingleErrorReport(
                catalogFile.FullName,
                CliDiagnosticCodes.FileReadFailed,
                exception.Message);
            WriteExecutionError(report, writeJson, standardOutput, standardError);
            return CliExitCodes.ExecutionError;
        }

        I18nCatalog catalog;
        try
        {
            catalog = I18nCatalogJson.Deserialize(json);
        }
        catch (JsonReaderException exception)
        {
            ValidationReport report = CreateInvalidJsonReport(
                catalogFile.FullName,
                exception.Message,
                exception.LineNumber,
                exception.LinePosition);
            WriteValidationReport(report, writeJson, standardOutput);
            return CliExitCodes.InvalidData;
        }
        catch (JsonSerializationException exception)
        {
            ValidationReport report = CreateInvalidJsonReport(
                catalogFile.FullName,
                exception.Message,
                null,
                null);
            WriteValidationReport(report, writeJson, standardOutput);
            return CliExitCodes.InvalidData;
        }

        I18nValidationResult validation = I18nCatalogValidator.Validate(catalog);
        ValidationReport validationReport = CreateValidationReport(catalogFile.FullName, validation);
        WriteValidationReport(validationReport, writeJson, standardOutput);

        return !validation.HasErrors
            ? CliExitCodes.Success
            : CliExitCodes.InvalidData;
    }

    private static ValidationReport CreateValidationReport(
        string file,
        I18nValidationResult validation)
    {
        return new ValidationReport
        {
            File = file,
            ErrorCount = validation.ErrorCount,
            WarningCount = validation.WarningCount,
            Diagnostics = validation.Diagnostics
                .Select(diagnostic => new ValidationDiagnosticReport
                {
                    Code = diagnostic.Code,
                    Severity = diagnostic.Severity.ToString().ToLowerInvariant(),
                    JsonPath = diagnostic.JsonPath,
                    Message = diagnostic.Message,
                    Target = CreateTargetReport(diagnostic.Target),
                })
                .ToArray(),
        };
    }

    private static ValidationTargetReport? CreateTargetReport(I18nValidationTarget target)
    {
        if (target.EntryId == null && target.EntryPath == null && target.LocaleId == null)
        {
            return null;
        }

        return new ValidationTargetReport
        {
            EntryId = target.EntryId,
            EntryPath = target.EntryPath,
            LocaleId = target.LocaleId,
        };
    }

    private static ValidationReport CreateInvalidJsonReport(
        string file,
        string message,
        int? line,
        int? position)
    {
        return new ValidationReport
        {
            File = file,
            ErrorCount = 1,
            WarningCount = 0,
            Diagnostics = new[]
            {
                new ValidationDiagnosticReport
                {
                    Code = CliDiagnosticCodes.InvalidJson,
                    Severity = "error",
                    JsonPath = "$",
                    Message = message,
                    Line = line,
                    Position = position,
                },
            },
        };
    }

    private static ValidationReport CreateSingleErrorReport(
        string file,
        string code,
        string message)
    {
        return new ValidationReport
        {
            File = file,
            ErrorCount = 1,
            WarningCount = 0,
            Diagnostics = new[]
            {
                new ValidationDiagnosticReport
                {
                    Code = code,
                    Severity = "error",
                    JsonPath = "$",
                    Message = message,
                },
            },
        };
    }

    private static void WriteValidationReport(
        ValidationReport report,
        bool writeJson,
        TextWriter standardOutput)
    {
        if (writeJson)
        {
            standardOutput.WriteLine(report.ToJson());
            return;
        }

        string summary =
            $"{FormatCount(report.ErrorCount, "error")}, " +
            $"{FormatCount(report.WarningCount, "warning")}.";

        standardOutput.WriteLine(report.HasErrors
            ? $"Invalid: {summary}"
            : $"Valid: {summary}");

        WriteHumanDiagnostics(report, standardOutput);
    }

    private static void WriteExecutionError(
        ValidationReport report,
        bool writeJson,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        if (writeJson)
        {
            standardOutput.WriteLine(report.ToJson());
            return;
        }

        WriteHumanDiagnostics(report, standardError);
    }

    private static void WriteHumanDiagnostics(ValidationReport report, TextWriter writer)
    {
        foreach (ValidationDiagnosticReport diagnostic in report.Diagnostics)
        {
            writer.WriteLine(
                $"{diagnostic.Severity.ToUpperInvariant()} [{diagnostic.Code}] " +
                $"{diagnostic.JsonPath}: {diagnostic.Message}");
        }
    }

    private static string FormatCount(int count, string singular)
    {
        return $"{count} {(count == 1 ? singular : singular + "s")}";
    }
}
