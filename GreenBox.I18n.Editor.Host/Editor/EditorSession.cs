using GreenBox.I18n.Editor.Host.Contracts;

namespace GreenBox.I18n.Editor.Host.Editor;

/// <summary>
/// Owns the server-side state of the current editor session.
/// </summary>
public sealed class EditorSession
{
    private readonly Lock _lock = new();
    private I18nCatalog? _catalog;
    private string? _catalogPath;
    private long _revision = 0;

    /// <summary>
    /// Creates an immutable snapshot of the current session state.
    /// </summary>
    /// <returns>The current session snapshot.</returns>
    public EditorSessionResponse GetSnapshot()
    {
        lock (_lock)
        {
            return CreateSnapshot();
        }
    }

    /// <summary>
    /// Replaces the current working copy with a loaded catalog.
    /// </summary>
    /// <param name="catalogPath">The absolute path of the catalog source file.</param>
    /// <param name="catalog">The loaded and validated catalog.</param>
    /// <returns>A snapshot of the updated session state.</returns>
    public EditorSessionResponse Open(string catalogPath, I18nCatalog catalog)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogPath);
        ArgumentNullException.ThrowIfNull(catalog);

        lock (_lock)
        {
            _catalogPath = catalogPath;
            _catalog = catalog;
            _revision++;
            return CreateSnapshot();
        }
    }

    private EditorSessionResponse CreateSnapshot()
    {
        return new EditorSessionResponse(
            _catalog != null,
            _revision,
            _catalogPath,
            _catalog?.DefaultLocale,
            _catalog?.Locales?.Count ?? 0,
            _catalog?.Entries?.Count ?? 0);
    }
}
