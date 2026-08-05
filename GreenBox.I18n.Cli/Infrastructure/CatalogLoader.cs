using GreenBox.I18n;
using Newtonsoft.Json;

namespace GreenBox.I18n.Cli;

internal static class CatalogLoader
{
    public static CatalogLoadResult Load(FileInfo catalogFile)
    {
        if (!catalogFile.Exists)
        {
            return CatalogLoadResult.Failure(
                CliExitCodes.ExecutionError,
                CliDiagnosticCodes.FileNotFound,
                $"Catalog file was not found: {catalogFile.FullName}");
        }

        string json;
        try
        {
            json = File.ReadAllText(catalogFile.FullName);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return CatalogLoadResult.Failure(
                CliExitCodes.ExecutionError,
                CliDiagnosticCodes.FileReadFailed,
                exception.Message);
        }

        I18nCatalog catalog;
        try
        {
            catalog = I18nCatalogJson.Deserialize(json);
        }
        catch (JsonReaderException exception)
        {
            return CatalogLoadResult.Failure(
                CliExitCodes.InvalidData,
                CliDiagnosticCodes.InvalidJson,
                exception.Message,
                exception.LineNumber,
                exception.LinePosition);
        }
        catch (JsonSerializationException exception)
        {
            return CatalogLoadResult.Failure(
                CliExitCodes.InvalidData,
                CliDiagnosticCodes.InvalidJson,
                exception.Message);
        }

        I18nValidationResult validation = I18nCatalogValidator.Validate(catalog);
        if (validation.HasErrors)
        {
            return CatalogLoadResult.Failure(
                CliExitCodes.InvalidData,
                CliDiagnosticCodes.InvalidCatalog,
                $"Catalog contains {validation.ErrorCount} validation " +
                (validation.ErrorCount == 1 ? "error" : "errors") + ". Run 'i18n validate' for details.");
        }

        return CatalogLoadResult.Success(catalog);
    }
}

internal sealed class CatalogLoadResult
{
    private CatalogLoadResult(
        I18nCatalog? catalog,
        int exitCode,
        CliError? error)
    {
        Catalog = catalog;
        ExitCode = exitCode;
        Error = error;
    }

    public I18nCatalog? Catalog { get; }

    public int ExitCode { get; }

    public CliError? Error { get; }

    public static CatalogLoadResult Success(I18nCatalog catalog)
    {
        return new CatalogLoadResult(catalog, CliExitCodes.Success, null);
    }

    public static CatalogLoadResult Failure(
        int exitCode,
        string code,
        string message,
        int? line = null,
        int? position = null)
    {
        return new CatalogLoadResult(
            null,
            exitCode,
            new CliError
            {
                Code = code,
                Message = message,
                Line = line,
                Position = position,
            });
    }
}
