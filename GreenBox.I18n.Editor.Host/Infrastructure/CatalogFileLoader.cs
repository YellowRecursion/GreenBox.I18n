using GreenBox.I18n.Editor.Host.Editor;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace GreenBox.I18n.Editor.Host.Infrastructure;

/// <summary>
/// Loads and validates source catalog files from the local file system.
/// </summary>
public sealed class CatalogFileLoader
{
    /// <summary>
    /// Loads a catalog file without modifying the current editor session.
    /// </summary>
    /// <param name="path">The catalog path supplied by the client.</param>
    /// <param name="cancellationToken">A token used to cancel file reading.</param>
    /// <returns>The catalog load result.</returns>
    public async Task<CatalogLoadResult> LoadAsync(string? path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return CatalogLoadResult.Failure(
                EditorErrorCodes.MissingCatalogPath,
                "A catalog path is required.");
        }

        string catalogPath;
        try
        {
            catalogPath = Path.GetFullPath(path.Trim());
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return CatalogLoadResult.Failure(EditorErrorCodes.InvalidCatalogPath, exception.Message);
        }

        if (!File.Exists(catalogPath))
        {
            return CatalogLoadResult.Failure(
                EditorErrorCodes.CatalogNotFound,
                $"Catalog file was not found: {catalogPath}");
        }

        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(catalogPath, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return CatalogLoadResult.Failure(EditorErrorCodes.CatalogReadFailed, exception.Message);
        }

        I18nCatalog catalog;
        try
        {
            string json = new UTF8Encoding(false, true).GetString(bytes);
            catalog = I18nCatalogJson.Deserialize(json);
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException)
        {
            return CatalogLoadResult.Failure(EditorErrorCodes.InvalidCatalogJson, exception.Message);
        }

        I18nValidationResult validation = I18nCatalogValidator.Validate(catalog);
        if (validation.HasErrors)
        {
            return CatalogLoadResult.Failure(
                EditorErrorCodes.InvalidCatalog,
                $"Catalog contains {validation.ErrorCount} validation " +
                (validation.ErrorCount == 1 ? "error." : "errors."));
        }

        return CatalogLoadResult.Success(
            catalog,
            catalogPath,
            Convert.ToHexString(SHA256.HashData(bytes)));
    }
}
