using GreenBox.I18n.Editor.Host.Contracts;
using System.Globalization;

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
    private readonly HashSet<string> _dirtyEntryIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dirtyPaths = new(StringComparer.Ordinal);
    private readonly HashSet<string> _createdEntryIds = new(StringComparer.Ordinal);

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
    /// Creates an immutable client-facing snapshot of the catalog working copy.
    /// </summary>
    /// <returns>The catalog snapshot, or <see langword="null"/> when no catalog is open.</returns>
    public CatalogResponse? GetCatalogSnapshot()
    {
        lock (_lock)
        {
            if (_catalog == null)
            {
                return null;
            }

            return CreateCatalogResponse();
        }
    }

    /// <summary>
    /// Adds an entry to the current working copy.
    /// </summary>
    /// <param name="path">The full logical path of the new entry.</param>
    /// <returns>The operation result and updated snapshot.</returns>
    public CatalogEditResult AddEntry(string? path)
    {
        lock (_lock)
        {
            if (_catalog == null)
            {
                return CatalogEditResult.Failure(
                    EditorErrorCodes.CatalogNotOpen,
                    "No catalog is open in the editor session.");
            }

            I18nEditResult editResult = _catalog.AddEntry(path);
            if (!editResult.IsSuccess)
            {
                return CatalogEditResult.Failure(editResult.Error!.Code, editResult.Error.Message);
            }

            if (editResult.HasChanges)
            {
                _dirtyEntryIds.Add(editResult.Entry!.Id);
                _dirtyPaths.Add(editResult.Entry.Path);
                _createdEntryIds.Add(editResult.Entry.Id);
                _revision++;
            }

            return CatalogEditResult.Success(CreateCatalogResponse());
        }
    }

    /// <summary>
    /// Removes an entry from the current working copy.
    /// </summary>
    /// <param name="id">The stable numeric ID of the entry to remove.</param>
    /// <returns>The operation result and updated snapshot.</returns>
    public CatalogEditResult RemoveEntry(long id)
    {
        return RemoveEntries(new[] { id.ToString(CultureInfo.InvariantCulture) });
    }

    /// <summary>
    /// Removes entries from the current working copy as one editor operation.
    /// </summary>
    /// <param name="ids">The stable decimal IDs of entries to remove.</param>
    /// <returns>The operation result and updated snapshot.</returns>
    public CatalogEditResult RemoveEntries(IReadOnlyCollection<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);

        lock (_lock)
        {
            if (_catalog == null)
            {
                return CatalogEditResult.Failure(
                    EditorErrorCodes.CatalogNotOpen,
                    "No catalog is open in the editor session.");
            }

            var uniqueIds = new HashSet<long>();
            foreach (string idText in ids)
            {
                if (!long.TryParse(
                        idText,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out long id) ||
                    id <= 0)
                {
                    return CatalogEditResult.Failure(
                        I18nEditCodes.InvalidId,
                        $"Entry ID must be a positive 64-bit integer: '{idText}'.");
                }

                uniqueIds.Add(id);
            }

            var entries = new List<I18nEntry>(uniqueIds.Count);
            foreach (long id in uniqueIds)
            {
                I18nEntry? entry = _catalog.FindById(id);
                if (entry == null)
                {
                    return CatalogEditResult.Failure(
                        I18nEditCodes.EntryNotFound,
                        $"Entry with ID {id} was not found.");
                }

                entries.Add(entry);
            }

            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                I18nEntry entry = entries[entryIndex];
                I18nEditResult editResult = _catalog.RemoveEntry(
                    long.Parse(entry.Id, CultureInfo.InvariantCulture));
                if (!editResult.IsSuccess)
                {
                    return CatalogEditResult.Failure(editResult.Error!.Code, editResult.Error.Message);
                }

                _dirtyEntryIds.Remove(entry.Id);
                if (_createdEntryIds.Remove(entry.Id))
                {
                    _dirtyPaths.Remove(entry.Path);
                }
                else
                {
                    _dirtyPaths.Add(entry.Path);
                }
            }

            if (entries.Count > 0)
            {
                _revision++;
            }

            return CatalogEditResult.Success(CreateCatalogResponse());
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
            _dirtyEntryIds.Clear();
            _dirtyPaths.Clear();
            _createdEntryIds.Clear();
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

    private CatalogResponse CreateCatalogResponse()
    {
        I18nValidationResult validation = I18nCatalogValidator.Validate(_catalog);
        return new CatalogResponse(
            _revision,
            _catalog!.DefaultLocale,
            _catalog.Locales.Select(CreateLocaleResponse).ToArray(),
            _catalog.Entries.Select(CreateEntryResponse).ToArray(),
            validation.Diagnostics.Select(CreateDiagnosticResponse).ToArray(),
            _dirtyEntryIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            _dirtyPaths.OrderBy(path => path, StringComparer.Ordinal).ToArray());
    }

    private static CatalogLocaleResponse CreateLocaleResponse(I18nLocaleDefinition locale)
    {
        return new CatalogLocaleResponse(
            locale.Id,
            locale.DisplayName,
            locale.Culture,
            locale.Fallback,
            CreateAssetResponse(locale.Icon));
    }

    private static CatalogEntryResponse CreateEntryResponse(I18nEntry entry)
    {
        Dictionary<string, CatalogLocaleValueResponse> locales = entry.Locales.ToDictionary(
            pair => pair.Key,
            pair => new CatalogLocaleValueResponse(
                pair.Value.Text,
                CreateAssetResponse(pair.Value.Asset)),
            StringComparer.Ordinal);

        return new CatalogEntryResponse(entry.Id, entry.Path, entry.Comment, locales);
    }

    private static CatalogAssetReferenceResponse? CreateAssetResponse(I18nAssetReference? asset)
    {
        return asset == null
            ? null
            : new CatalogAssetReferenceResponse(asset.AssetGuid, asset.LocalFileId);
    }

    private static CatalogDiagnosticResponse CreateDiagnosticResponse(I18nValidationDiagnostic diagnostic)
    {
        I18nValidationTarget target = diagnostic.Target;
        CatalogDiagnosticTargetResponse? targetResponse =
            target.EntryId == null && target.EntryPath == null && target.LocaleId == null
                ? null
                : new CatalogDiagnosticTargetResponse(target.EntryId, target.EntryPath, target.LocaleId);

        return new CatalogDiagnosticResponse(
            diagnostic.Code,
            diagnostic.Severity.ToString().ToLowerInvariant(),
            diagnostic.JsonPath,
            diagnostic.Message,
            targetResponse);
    }
}
