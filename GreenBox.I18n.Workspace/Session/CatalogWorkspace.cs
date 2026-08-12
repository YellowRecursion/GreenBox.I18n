using GreenBox.I18n.Workspace.Contracts;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace GreenBox.I18n.Workspace;

/// <summary>
/// Owns the state of the active catalog working copy.
/// </summary>
public sealed partial class CatalogWorkspace
{
    private readonly Lock _lock = new();
    private I18nCatalog? _catalog;
    private I18nCatalog? _baselineCatalog;
    private string? _catalogPath;
    private string? _baselineHash;
    private long _revision = 0;
    private readonly HashSet<string> _dirtyLocaleIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dirtyEntryIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dirtyPaths = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PreparedWorkspaceChangeSet> _changeSets = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates an immutable snapshot of the current session state.
    /// </summary>
    /// <returns>The current session snapshot.</returns>
    public WorkspaceResponse GetSnapshot()
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
            return GetSourceStatusUnsafe();
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
                    WorkspaceErrorCodes.CatalogNotOpen,
                    "No catalog is open in the workspace.");
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
                    WorkspaceErrorCodes.CatalogNotOpen,
                    "No catalog is open in the workspace.");
            }

            var uniqueIds = new HashSet<long>();
            foreach (string idText in ids)
            {
                if (!I18nEntryId.TryParse(idText, out long id))
                {
                    return CatalogEditResult.Failure(
                        I18nEditCodes.InvalidId,
                        $"Entry ID does not use the GreenBox I18n ID format: '{idText}'.");
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
                    WorkspaceErrorCodes.CatalogNotOpen,
                    "No catalog is open in the workspace.");
            }

            var coreMoves = new List<I18nEntryMove>(moves.Count);
            foreach (MoveCatalogEntryRequest move in moves)
            {
                if (!I18nEntryId.TryParse(move.Id, out long id))
                {
                    return CatalogEditResult.Failure(
                        I18nEditCodes.InvalidId,
                        $"Entry ID does not use the GreenBox I18n ID format: '{move.Id}'.");
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
    /// Atomically restores complete entry values and removes entries by stable ID.
    /// </summary>
    /// <param name="expectedRevision">The working-copy revision on which the delta is based.</param>
    /// <param name="entries">The entries to add or replace.</param>
    /// <param name="removedIds">The IDs that must be absent after the operation.</param>
    /// <returns>The operation result and updated snapshot.</returns>
    public CatalogEditResult ApplyEntryDelta(
        long expectedRevision,
        IReadOnlyCollection<CatalogEntryEditRequest> entries,
        IReadOnlyCollection<string> removedIds)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(removedIds);

        lock (_lock)
        {
            if (_catalog == null)
            {
                return CatalogEditResult.Failure(
                    WorkspaceErrorCodes.CatalogNotOpen,
                    "No catalog is open in the workspace.");
            }

            if (expectedRevision != _revision)
            {
                return CatalogEditResult.Failure(
                    WorkspaceErrorCodes.CatalogRevisionMismatch,
                    $"The entry delta expected revision {expectedRevision}, but the working copy is at revision {_revision}.");
            }

            var parsedRemovedIds = new List<long>(removedIds.Count);
            foreach (string id in removedIds)
            {
                if (!I18nEntryId.TryParse(id, out long numericId))
                {
                    return CatalogEditResult.Failure(
                        I18nEditCodes.InvalidId,
                        $"Entry ID does not use the GreenBox I18n ID format: '{id}'.");
                }

                parsedRemovedIds.Add(numericId);
            }

            I18nBatchEditResult editResult = _catalog.ApplyEntryDelta(
                entries.Select(CreateEntry).ToArray(),
                parsedRemovedIds);
            if (!editResult.IsSuccess)
            {
                return CatalogEditResult.Failure(
                    editResult.Error!.Code,
                    editResult.Error.Message);
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
    /// Atomically replaces locale definitions and the catalog default locale.
    /// </summary>
    /// <param name="expectedRevision">The working-copy revision on which the edit is based.</param>
    /// <param name="defaultLocale">The locale identifier used as the catalog default.</param>
    /// <param name="locales">The complete locale definitions in editor display order.</param>
    /// <param name="renames">Locale ID changes to propagate through entry values.</param>
    /// <param name="removedIds">Locale IDs to remove from definitions and entries.</param>
    /// <returns>The operation result and updated snapshot.</returns>
    public CatalogEditResult ApplyLocales(
        long expectedRevision,
        string defaultLocale,
        IReadOnlyCollection<CatalogLocaleEditRequest> locales,
        IReadOnlyCollection<CatalogLocaleRenameRequest> renames,
        IReadOnlyCollection<string> removedIds)
    {
        ArgumentNullException.ThrowIfNull(locales);
        ArgumentNullException.ThrowIfNull(renames);
        ArgumentNullException.ThrowIfNull(removedIds);

        lock (_lock)
        {
            if (_catalog == null)
            {
                return CatalogEditResult.Failure(
                    WorkspaceErrorCodes.CatalogNotOpen,
                    "No catalog is open in the workspace.");
            }

            if (expectedRevision != _revision)
            {
                return CatalogEditResult.Failure(
                    WorkspaceErrorCodes.CatalogRevisionMismatch,
                    $"The locale edit expected revision {expectedRevision}, but the working copy is at revision {_revision}.");
            }

            I18nCatalog candidate = CloneCatalog(_catalog);
            foreach (CatalogLocaleRenameRequest rename in renames)
            {
                CatalogEditResult? renameError = RenameLocaleReferences(candidate, rename);
                if (renameError != null)
                {
                    return renameError;
                }
            }

            foreach (string removedId in removedIds.Distinct(StringComparer.Ordinal))
            {
                CatalogEditResult? removalError = RemoveLocaleReferences(candidate, removedId);
                if (removalError != null)
                {
                    return removalError;
                }
            }

            candidate.DefaultLocale = defaultLocale;
            candidate.Locales = locales.Select(CreateLocale).ToList();

            I18nValidationResult validation = I18nCatalogValidator.Validate(candidate);
            if (validation.HasErrors)
            {
                I18nValidationDiagnostic diagnostic = validation.Diagnostics.First(item =>
                    item.Severity == I18nValidationSeverity.Error);
                return CatalogEditResult.Failure(diagnostic.Code, diagnostic.Message);
            }

            if (I18nCatalogJson.Serialize(candidate) != I18nCatalogJson.Serialize(_catalog))
            {
                _catalog = candidate;
                RebuildDirtyState();
                _revision++;
            }

            return CatalogEditResult.Success(CreateCatalogResponse());
        }
    }

    private static CatalogEditResult? RemoveLocaleReferences(I18nCatalog catalog, string localeId)
    {
        I18nLocaleDefinition? locale = catalog.Locales.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, localeId, StringComparison.Ordinal));
        if (locale == null)
        {
            return CatalogEditResult.Failure(
                I18nValidationCodes.InvalidLocaleId,
                $"Locale '{localeId}' does not exist.");
        }

        catalog.Locales.Remove(locale);
        foreach (I18nLocaleDefinition candidate in catalog.Locales)
        {
            if (string.Equals(candidate.Fallback, localeId, StringComparison.Ordinal))
            {
                candidate.Fallback = null;
            }
        }

        foreach (I18nEntry entry in catalog.Entries)
        {
            entry.Locales.Remove(localeId);
        }

        return null;
    }

    private static CatalogEditResult? RenameLocaleReferences(
        I18nCatalog catalog,
        CatalogLocaleRenameRequest rename)
    {
        if (string.Equals(rename.FromId, rename.ToId, StringComparison.Ordinal))
        {
            return null;
        }

        I18nLocaleDefinition? source = catalog.Locales.FirstOrDefault(locale =>
            string.Equals(locale.Id, rename.FromId, StringComparison.Ordinal));
        if (source == null)
        {
            return CatalogEditResult.Failure(
                I18nValidationCodes.InvalidLocaleId,
                $"Locale '{rename.FromId}' does not exist.");
        }

        if (catalog.Locales.Any(locale =>
            string.Equals(locale.Id, rename.ToId, StringComparison.Ordinal)))
        {
            return CatalogEditResult.Failure(
                I18nValidationCodes.DuplicateLocaleId,
                $"Locale ID '{rename.ToId}' is already used.");
        }

        source.Id = rename.ToId;
        if (string.Equals(catalog.DefaultLocale, rename.FromId, StringComparison.Ordinal))
        {
            catalog.DefaultLocale = rename.ToId;
        }

        foreach (I18nLocaleDefinition locale in catalog.Locales)
        {
            if (string.Equals(locale.Fallback, rename.FromId, StringComparison.Ordinal))
            {
                locale.Fallback = rename.ToId;
            }
        }

        foreach (I18nEntry entry in catalog.Entries)
        {
            if (!entry.Locales.TryGetValue(rename.FromId, out I18nLocaleValue? value))
            {
                continue;
            }

            if (entry.Locales.ContainsKey(rename.ToId))
            {
                return CatalogEditResult.Failure(
                    I18nValidationCodes.DuplicateLocaleId,
                    $"Entry '{entry.Path}' already contains locale '{rename.ToId}'.");
            }

            entry.Locales.Remove(rename.FromId);
            entry.Locales.Add(rename.ToId, value);
        }

        return null;
    }

    /// <summary>
    /// Replaces the current working copy with a loaded catalog.
    /// </summary>
    /// <param name="catalogPath">The absolute path of the catalog source file.</param>
    /// <param name="catalog">The loaded and validated catalog.</param>
    /// <param name="contentHash">The SHA-256 hash of the loaded source file.</param>
    /// <returns>A snapshot of the updated session state.</returns>
    public WorkspaceResponse Open(string catalogPath, I18nCatalog catalog, string contentHash)
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
            _changeSets.Clear();
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
                    WorkspaceErrorCodes.CatalogNotOpen,
                    "No catalog is open in the workspace.");
            }

            I18nValidationResult validation = I18nCatalogValidator.Validate(_catalog);
            if (validation.HasErrors)
            {
                return CatalogEditResult.Failure(
                    WorkspaceErrorCodes.InvalidCatalog,
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
                        WorkspaceErrorCodes.CatalogChangedExternally,
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
                    WorkspaceErrorCodes.CatalogWriteFailed,
                    exception.Message);
            }

            ClearDirtyState();
            _revision++;
            return CatalogEditResult.Success(CreateCatalogResponse());
        }
    }

    private void ClearDirtyState()
    {
        _dirtyLocaleIds.Clear();
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
                    new I18nCatalogMergeConflict("$", "No catalog is open in the workspace."),
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

        Dictionary<string, I18nLocaleDefinition> currentLocalesById = _catalog.Locales.ToDictionary(
            locale => locale.Id,
            StringComparer.Ordinal);
        Dictionary<string, I18nLocaleDefinition> baselineLocalesById = _baselineCatalog.Locales.ToDictionary(
            locale => locale.Id,
            StringComparer.Ordinal);
        foreach (string id in currentLocalesById.Keys.Concat(baselineLocalesById.Keys).Distinct(StringComparer.Ordinal))
        {
            currentLocalesById.TryGetValue(id, out I18nLocaleDefinition? current);
            baselineLocalesById.TryGetValue(id, out I18nLocaleDefinition? baseline);
            if (!LocaleEquals(current, baseline))
            {
                _dirtyLocaleIds.Add(id);
            }
        }

        if (!string.Equals(_catalog.DefaultLocale, _baselineCatalog.DefaultLocale, StringComparison.Ordinal))
        {
            _dirtyLocaleIds.Add(_catalog.DefaultLocale);
            _dirtyLocaleIds.Add(_baselineCatalog.DefaultLocale);
        }
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

    private static bool LocaleEquals(I18nLocaleDefinition? left, I18nLocaleDefinition? right)
    {
        return ReferenceEquals(left, right) || left != null && right != null &&
            left.Id == right.Id &&
            left.DisplayName == right.DisplayName &&
            left.Culture == right.Culture &&
            left.Fallback == right.Fallback &&
            AssetEquals(left.Icon, right.Icon);
    }

    private static bool AssetEquals(I18nAssetReference? left, I18nAssetReference? right)
    {
        return ReferenceEquals(left, right) || left != null && right != null &&
            left.AssetGuid == right.AssetGuid && left.LocalFileId == right.LocalFileId;
    }

    private static I18nEntry CreateEntry(CatalogEntryEditRequest source)
    {
        return new I18nEntry
        {
            Id = source.Id,
            Path = source.Path,
            Comment = source.Comment,
            Locales = source.Locales.ToDictionary(
                pair => pair.Key,
                pair => new I18nLocaleValue
                {
                    Text = pair.Value.Text,
                    Asset = pair.Value.Asset == null
                        ? null
                        : new I18nAssetReference
                        {
                            AssetGuid = pair.Value.Asset.AssetGuid,
                            LocalFileId = pair.Value.Asset.LocalFileId,
                        },
                },
                StringComparer.Ordinal),
        };
    }

    private static I18nLocaleDefinition CreateLocale(CatalogLocaleEditRequest source)
    {
        return new I18nLocaleDefinition
        {
            Id = source.Id,
            DisplayName = source.DisplayName,
            Culture = source.Culture,
            Fallback = source.Fallback,
            Icon = source.Icon == null
                ? null
                : new I18nAssetReference
                {
                    AssetGuid = source.Icon.AssetGuid,
                    LocalFileId = source.Icon.LocalFileId,
                },
        };
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

    private WorkspaceResponse CreateSnapshot()
    {
        return new WorkspaceResponse(
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
            _dirtyLocaleIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            _dirtyEntryIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            _dirtyPaths.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
            HasChangesUnsafe());
    }

    private bool HasChangesUnsafe() =>
        _baselineCatalog == null ||
        _dirtyLocaleIds.Count != 0 ||
        _dirtyEntryIds.Count != 0 ||
        _dirtyPaths.Count != 0;

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
