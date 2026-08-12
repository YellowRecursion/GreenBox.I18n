using GreenBox.I18n.Workspace.Contracts;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace GreenBox.I18n.Workspace;

/// <summary>
/// Loads and validates source catalog files from the local file system.
/// </summary>
public sealed class CatalogFileLoader
{
    /// <summary>
    /// Loads a catalog file without modifying the current workspace.
    /// </summary>
    /// <param name="path">The catalog path supplied by the client.</param>
    /// <param name="cancellationToken">A token used to cancel file reading.</param>
    /// <returns>The catalog load result.</returns>
    public async Task<CatalogLoadResult> LoadAsync(string? path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return CatalogLoadResult.Failure(
                WorkspaceErrorCodes.MissingCatalogPath,
                "A catalog path is required.");
        }

        string catalogPath;
        try
        {
            catalogPath = Path.GetFullPath(path.Trim());
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return CatalogLoadResult.Failure(WorkspaceErrorCodes.InvalidCatalogPath, exception.Message);
        }

        if (!File.Exists(catalogPath))
        {
            return CatalogLoadResult.Failure(
                WorkspaceErrorCodes.CatalogNotFound,
                $"Catalog file was not found: {catalogPath}");
        }

        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(catalogPath, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return CatalogLoadResult.Failure(WorkspaceErrorCodes.CatalogReadFailed, exception.Message);
        }

        I18nCatalog catalog;
        try
        {
            string json = new UTF8Encoding(false, true).GetString(bytes);
            catalog = I18nCatalogJson.Deserialize(json);
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException)
        {
            return CatalogLoadResult.Failure(WorkspaceErrorCodes.InvalidCatalogJson, exception.Message);
        }

        I18nValidationResult validation = I18nCatalogValidator.Validate(catalog);
        if (validation.HasErrors)
        {
            return CatalogLoadResult.Failure(
                WorkspaceErrorCodes.InvalidCatalog,
                $"Catalog contains {validation.ErrorCount} validation " +
                (validation.ErrorCount == 1 ? "error." : "errors."));
        }

        return CatalogLoadResult.Success(
            catalog,
            catalogPath,
            Convert.ToHexString(SHA256.HashData(bytes)));
    }
}
