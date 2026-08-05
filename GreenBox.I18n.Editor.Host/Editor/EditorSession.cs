using GreenBox.I18n.Editor.Host.Contracts;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace GreenBox.I18n.Editor.Host.Editor;

/// <summary>
/// Owns the server-side state of the current editor session.
/// </summary>
public sealed class EditorSession
{
    private readonly Lock _lock = new();
    private I18nCatalog? _catalog;
    private I18nCatalog? _baselineCatalog;
    private string? _catalogPath;
    private string? _baselineHash;
    private long _revision = 0;
    private readonly HashSet<string> _dirtyEntryIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dirtyPaths = new(StringComparer.Ordinal);

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
    /// Compares the catalog source file with the version used by the working copy.
    /// </summary>
    /// <returns>The current source file status.</returns>
    public CatalogSourceStatusResponse GetSourceStatus()
    {
        lock (_lock)
        {
            if (_catalogPath == null || _baselineHash == null)
            {
                return new CatalogSourceStatusResponse(false, false, "No catalog is open in the editor session.");
            }

            try
            {
                byte[] sourceBytes = File.ReadAllBytes(_catalogPath);
                string sourceHash = Convert.ToHexString(SHA256.HashData(sourceBytes));
                return new CatalogSourceStatusResponse(
                    !string.Equals(sourceHash, _baselineHash, StringComparison.Ordinal),
                    true,
                    null);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return new CatalogSourceStatusResponse(true, false, exception.Message);
            }
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
                RebuildDirtyState();
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

            }

            if (entries.Count > 0)
            {
                RebuildDirtyState();
                _revision++;
            }

            return CatalogEditResult.Success(CreateCatalogResponse());
        }
    }

    /// <summary>
    /// Changes several entry paths as one editor operation.
    /// </summary>
    /// <param name="moves">The stable entry IDs and destination paths.</param>
    /// <returns>The operation result and updated snapshot.</returns>
    public CatalogEditResult MoveEntries(IReadOnlyCollection<MoveCatalogEntryRequest> moves)
    {
        ArgumentNullException.ThrowIfNull(moves);

        lock (_lock)
        {
            if (_catalog == null)
            {
                return CatalogEditResult.Failure(
                    EditorErrorCodes.CatalogNotOpen,
                    "No catalog is open in the editor session.");
            }

            var coreMoves = new List<I18nEntryMove>(moves.Count);
            foreach (MoveCatalogEntryRequest move in moves)
            {
                if (!long.TryParse(
                        move.Id,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out long id) ||
                    id <= 0)
                {
                    return CatalogEditResult.Failure(
                        I18nEditCodes.InvalidId,
                        $"Entry ID must be a positive 64-bit integer: '{move.Id}'.");
                }

                coreMoves.Add(new I18nEntryMove(id, move.Path));
            }

            I18nBatchEditResult editResult = _catalog.MoveEntries(coreMoves);
            if (!editResult.IsSuccess)
            {
                return CatalogEditResult.Failure(editResult.Error!.Code, editResult.Error.Message);
            }

            if (editResult.HasChanges)
            {
                RebuildDirtyState();
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
    /// <param name="contentHash">The SHA-256 hash of the loaded source file.</param>
    /// <returns>A snapshot of the updated session state.</returns>
    public EditorSessionResponse Open(string catalogPath, I18nCatalog catalog, string contentHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogPath);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        lock (_lock)
        {
            _catalogPath = catalogPath;
            _catalog = catalog;
            _baselineCatalog = CloneCatalog(catalog);
            _baselineHash = contentHash;
            ClearDirtyState();
            _revision++;
            return CreateSnapshot();
        }
    }

    /// <summary>
    /// Saves the current working copy to its source file.
    /// </summary>
    /// <param name="overwriteExternalChanges">Whether to overwrite a source file changed externally.</param>
    /// <returns>The operation result and updated catalog snapshot.</returns>
    public CatalogEditResult Save(bool overwriteExternalChanges)
    {
        lock (_lock)
        {
            if (_catalog == null || _catalogPath == null || _baselineHash == null)
            {
                return CatalogEditResult.Failure(
                    EditorErrorCodes.CatalogNotOpen,
                    "No catalog is open in the editor session.");
            }

            I18nValidationResult validation = I18nCatalogValidator.Validate(_catalog);
            if (validation.HasErrors)
            {
                return CatalogEditResult.Failure(
                    EditorErrorCodes.InvalidCatalog,
                    $"Catalog contains {validation.ErrorCount} validation " +
                    (validation.ErrorCount == 1 ? "error." : "errors."));
            }

            try
            {
                byte[] sourceBytes = File.ReadAllBytes(_catalogPath);
                string sourceHash = Convert.ToHexString(SHA256.HashData(sourceBytes));
                if (!overwriteExternalChanges &&
                    !string.Equals(sourceHash, _baselineHash, StringComparison.Ordinal))
                {
                    return CatalogEditResult.Failure(
                        EditorErrorCodes.CatalogChangedExternally,
                        "The catalog file changed on disk after it was loaded.");
                }

                byte[] savedBytes = new UTF8Encoding(false).GetBytes(I18nCatalogJson.Serialize(_catalog));
                WriteAtomically(_catalogPath, savedBytes);
                _baselineHash = Convert.ToHexString(SHA256.HashData(savedBytes));
                _baselineCatalog = CloneCatalog(_catalog);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return CatalogEditResult.Failure(
                    EditorErrorCodes.CatalogWriteFailed,
                    exception.Message);
            }

            ClearDirtyState();
            _revision++;
            return CatalogEditResult.Success(CreateCatalogResponse());
        }
    }

    private void ClearDirtyState()
    {
        _dirtyEntryIds.Clear();
        _dirtyPaths.Clear();
    }

    /// <summary>
    /// Safely merges a newly loaded source catalog into the current working copy.
    /// </summary>
    /// <param name="incoming">The current catalog content from disk.</param>
    /// <param name="contentHash">The SHA-256 hash of the incoming source file.</param>
    /// <returns>The merge result and updated working copy.</returns>
    public CatalogSourceMergeResult MergeSource(I18nCatalog incoming, string contentHash)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        lock (_lock)
        {
            if (_catalog == null || _baselineCatalog == null)
            {
                return CatalogSourceMergeResult.Failure(new[]
                {
                    new I18nCatalogMergeConflict("$", "No catalog is open in the editor session."),
                });
            }

            I18nCatalogMergeResult merge = I18nCatalogMerge.Merge(_baselineCatalog, _catalog, incoming);
            if (!merge.IsSuccess)
            {
                return CatalogSourceMergeResult.Failure(merge.Conflicts);
            }

            _catalog = merge.Catalog!;
            _baselineCatalog = CloneCatalog(incoming);
            _baselineHash = contentHash;
            RebuildDirtyState();
            _revision++;
            return CatalogSourceMergeResult.Success(CreateCatalogResponse());
        }
    }

    private void RebuildDirtyState()
    {
        ClearDirtyState();
        if (_catalog == null || _baselineCatalog == null)
        {
            return;
        }

        Dictionary<string, I18nEntry> currentById = _catalog.Entries.ToDictionary(
            entry => entry.Id,
            StringComparer.Ordinal);
        Dictionary<string, I18nEntry> baselineById = _baselineCatalog.Entries.ToDictionary(
            entry => entry.Id,
            StringComparer.Ordinal);

        foreach (string id in currentById.Keys.Concat(baselineById.Keys).Distinct(StringComparer.Ordinal))
        {
            currentById.TryGetValue(id, out I18nEntry? current);
            baselineById.TryGetValue(id, out I18nEntry? baseline);
            if (EntryEquals(current, baseline))
            {
                continue;
            }

            _dirtyEntryIds.Add(id);
            if (current != null) _dirtyPaths.Add(current.Path);
            if (baseline != null) _dirtyPaths.Add(baseline.Path);
        }
    }

    private static bool EntryEquals(I18nEntry? left, I18nEntry? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left == null || right == null || left.Id != right.Id || left.Path != right.Path || left.Comment != right.Comment ||
            left.Locales.Count != right.Locales.Count)
        {
            return false;
        }

        return left.Locales.All(pair =>
            right.Locales.TryGetValue(pair.Key, out I18nLocaleValue? value) &&
            pair.Value.Text == value.Text && AssetEquals(pair.Value.Asset, value.Asset));
    }

    private static bool AssetEquals(I18nAssetReference? left, I18nAssetReference? right)
    {
        return ReferenceEquals(left, right) || left != null && right != null &&
            left.AssetGuid == right.AssetGuid && left.LocalFileId == right.LocalFileId;
    }

    private static I18nCatalog CloneCatalog(I18nCatalog catalog)
    {
        return I18nCatalogJson.Deserialize(I18nCatalogJson.Serialize(catalog));
    }

    private static void WriteAtomically(string path, byte[] bytes)
    {
        string directory = Path.GetDirectoryName(path)!;
        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllBytes(temporaryPath, bytes);
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
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
            _dirtyPaths.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
            _baselineCatalog == null ||
            I18nCatalogJson.Serialize(_catalog) != I18nCatalogJson.Serialize(_baselineCatalog));
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
