using System.Diagnostics;
using System.Text.Json;
using GreenBox.I18n.Editor.Host.Contracts;
using GreenBox.I18n.Editor.Host.Editor;

namespace GreenBox.I18n.Editor.Host.Infrastructure;

/// <summary>
/// Resolves the small set of Unity asset references used by the open catalog.
/// </summary>
public sealed class UnityAssetReferenceService
{
    private const string ClipboardFormat = "greenbox.i18n.asset-reference";
    private const int ClipboardFormatVersion = 1;
    private readonly Lock _lock = new();
    private readonly UnityProjectLocator _projectLocator;
    private string? _cachedProjectRoot;
    private Dictionary<string, string> _pathsByGuid = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a Unity asset-reference service.
    /// </summary>
    /// <param name="projectLocator">The shared Unity project locator.</param>
    public UnityAssetReferenceService(UnityProjectLocator projectLocator)
    {
        _projectLocator = projectLocator;
    }

    /// <summary>
    /// Resolves an asset GUID to its current path in the Unity project.
    /// </summary>
    /// <param name="catalogPath">The open catalog path used to locate the Unity project.</param>
    /// <param name="assetGuid">The Unity asset GUID.</param>
    /// <returns>The resolved reference, or an error when it cannot be resolved.</returns>
    public UnityAssetReferenceResult Resolve(string? catalogPath, string? assetGuid)
    {
        string? projectRoot = _projectLocator.FindProjectRoot(catalogPath);
        if (projectRoot == null)
        {
            return UnityAssetReferenceResult.Failure(
                EditorErrorCodes.UnityProjectNotFound,
                "The open catalog is not located inside a Unity Assets directory.");
        }

        string resolvedProjectRoot = projectRoot;

        if (!IsAssetGuid(assetGuid))
        {
            return UnityAssetReferenceResult.Failure(
                EditorErrorCodes.UnityAssetNotFound,
                $"'{assetGuid}' is not a Unity asset GUID.");
        }

        lock (_lock)
        {
            EnsureCache(resolvedProjectRoot);
            if (!TryResolvePath(resolvedProjectRoot, assetGuid!, out string? assetPath))
            {
                RebuildCache(resolvedProjectRoot);
                if (!TryResolvePath(resolvedProjectRoot, assetGuid!, out assetPath))
                {
                    return UnityAssetReferenceResult.Failure(
                        EditorErrorCodes.UnityAssetNotFound,
                        $"Unity asset GUID '{assetGuid}' was not found in the current project.");
                }
            }

            return UnityAssetReferenceResult.Success(CreateResponse(assetGuid!, null, assetPath!, null));
        }
    }

    /// <summary>
    /// Resolves a browser drop against the exact object currently selected by Unity.
    /// </summary>
    /// <param name="catalogPath">The open catalog path used to locate the Unity project.</param>
    /// <param name="fileName">The browser-visible dropped file name.</param>
    /// <returns>The selected Unity object reference, or a mismatch error.</returns>
    public UnityAssetReferenceResult ResolveDrop(string? catalogPath, string? fileName)
    {
        string? projectRoot = _projectLocator.FindProjectRoot(catalogPath);
        if (projectRoot == null)
        {
            return UnityAssetReferenceResult.Failure(
                EditorErrorCodes.UnityProjectNotFound,
                "The open catalog is not located inside a Unity Assets directory.");
        }

        string resolvedProjectRoot = projectRoot;

        string selectionPath = Path.Combine(
            resolvedProjectRoot,
            "Library",
            "GreenBox.I18n",
            "active-selection.json");
        UnityAssetSelection? selection;
        try
        {
            selection = JsonSerializer.Deserialize<UnityAssetSelection>(
                File.ReadAllText(selectionPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return UnityAssetReferenceResult.Failure(
                EditorErrorCodes.UnitySelectionUnavailable,
                "Unity has not published an active asset selection. Select the asset in Unity and try again.");
        }

        if (selection == null ||
            selection.Format != ClipboardFormat ||
            selection.Version != ClipboardFormatVersion ||
            !IsAssetGuid(selection.AssetGuid))
        {
            return UnityAssetReferenceResult.Failure(
                EditorErrorCodes.UnitySelectionUnavailable,
                "Unity's active asset selection uses an unsupported format.");
        }

        UnityAssetReferenceResult resolved = Resolve(catalogPath, selection.AssetGuid);
        if (resolved.Reference == null)
        {
            return resolved;
        }

        bool pathMatches = string.Equals(
            NormalizeAssetPath(selection.AssetPath),
            NormalizeAssetPath(resolved.Reference.AssetPath),
            StringComparison.OrdinalIgnoreCase);
        bool fileNameMatches = string.Equals(
            Path.GetFileName(resolved.Reference.AssetPath),
            fileName,
            StringComparison.OrdinalIgnoreCase);
        if (!pathMatches || !fileNameMatches)
        {
            return UnityAssetReferenceResult.Failure(
                EditorErrorCodes.UnitySelectionMismatch,
                "The dropped file does not match the asset currently selected in Unity.");
        }

        return UnityAssetReferenceResult.Success(CreateResponse(
            selection.AssetGuid,
            selection.LocalFileId,
            resolved.Reference.AssetPath,
            selection.ObjectName));
    }

    /// <summary>
    /// Opens a Unity asset using the operating system's associated application.
    /// </summary>
    /// <param name="catalogPath">The open catalog path used to locate the Unity project.</param>
    /// <param name="assetGuid">The Unity asset GUID.</param>
    /// <returns>An error when the reference cannot be resolved or opened.</returns>
    public EditorErrorResponse? Open(string? catalogPath, string? assetGuid)
    {
        UnityAssetReferenceResult resolved = Resolve(catalogPath, assetGuid);
        if (resolved.Reference == null)
        {
            return resolved.Error;
        }

        string projectRoot = _projectLocator.FindProjectRoot(catalogPath)!;
        string absolutePath = Path.GetFullPath(Path.Combine(projectRoot, resolved.Reference.AssetPath));
        try
        {
            Process.Start(new ProcessStartInfo(absolutePath) { UseShellExecute = true });
            return null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new EditorErrorResponse(EditorErrorCodes.UnityAssetOpenFailed, exception.Message);
        }
    }

    private static UnityAssetReferenceResponse CreateResponse(
        string assetGuid,
        string? localFileId,
        string assetPath,
        string? objectName)
    {
        return new UnityAssetReferenceResponse(
            assetGuid,
            string.IsNullOrWhiteSpace(localFileId) ? null : localFileId,
            assetPath,
            Path.GetFileName(assetPath),
            objectName);
    }

    private void EnsureCache(string projectRoot)
    {
        if (!string.Equals(_cachedProjectRoot, projectRoot, StringComparison.OrdinalIgnoreCase))
        {
            RebuildCache(projectRoot);
        }
    }

    private bool TryResolvePath(string projectRoot, string assetGuid, out string? assetPath)
    {
        if (_pathsByGuid.TryGetValue(assetGuid, out assetPath))
        {
            string absolutePath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            if (File.Exists(absolutePath) || Directory.Exists(absolutePath))
            {
                return true;
            }
        }

        assetPath = null;
        return false;
    }

    private void RebuildCache(string projectRoot)
    {
        string assetsPath = Path.Combine(projectRoot, "Assets");
        var pathsByGuid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string metaPath in Directory.EnumerateFiles(assetsPath, "*.meta", SearchOption.AllDirectories))
        {
            string? assetGuid = ReadAssetGuid(metaPath);
            if (assetGuid == null)
            {
                continue;
            }

            string sourcePath = metaPath[..^".meta".Length];
            string assetPath = Path.GetRelativePath(projectRoot, sourcePath).Replace('\\', '/');
            pathsByGuid[assetGuid] = assetPath;
        }

        _cachedProjectRoot = projectRoot;
        _pathsByGuid = pathsByGuid;
    }

    private static string? ReadAssetGuid(string metaPath)
    {
        foreach (string line in File.ReadLines(metaPath))
        {
            if (line.StartsWith("guid: ", StringComparison.Ordinal))
            {
                string value = line["guid: ".Length..].Trim();
                return IsAssetGuid(value) ? value : null;
            }
        }

        return null;
    }

    private static bool IsAssetGuid(string? value)
    {
        return value is { Length: 32 } && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
    }

    private static string NormalizeAssetPath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }

    private sealed record UnityAssetSelection(
        string Format,
        int Version,
        string AssetGuid,
        string? LocalFileId,
        string AssetPath,
        string? ObjectName);
}

/// <summary>
/// Contains either a resolved Unity asset reference or a stable editor error.
/// </summary>
/// <param name="Reference">The resolved reference.</param>
/// <param name="Error">The resolution error.</param>
public sealed record UnityAssetReferenceResult(
    UnityAssetReferenceResponse? Reference,
    EditorErrorResponse? Error)
{
    /// <summary>Creates a successful result.</summary>
    public static UnityAssetReferenceResult Success(UnityAssetReferenceResponse reference) => new(reference, null);

    /// <summary>Creates a failed result.</summary>
    public static UnityAssetReferenceResult Failure(string code, string message) =>
        new(null, new EditorErrorResponse(code, message));
}
