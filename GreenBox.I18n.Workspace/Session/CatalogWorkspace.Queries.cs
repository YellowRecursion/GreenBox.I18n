using System.Globalization;
using System.Text;
using GreenBox.I18n.Workspace.Contracts;

namespace GreenBox.I18n.Workspace;

public sealed partial class CatalogWorkspace
{
    private const int DefaultPageSize = 50;
    private const int MaximumPageSize = 200;
    private const int MaximumEntryBatchSize = 50;
    private const int TextPreviewLength = 160;

    public WorkspaceContextResponse GetContext()
    {
        lock (_lock)
        {
            return new WorkspaceContextResponse(
                _catalog != null,
                _revision,
                _catalogPath,
                _catalog?.DefaultLocale,
                _catalog?.Locales.Select(CreateLocaleResponse).ToArray() ?? Array.Empty<CatalogLocaleResponse>(),
                _catalog?.Entries.Count ?? 0,
                _catalog != null && HasChangesUnsafe(),
                GetSourceStatusUnsafe());
        }
    }

    public WorkspaceEntrySearchResponse SearchEntries(WorkspaceEntrySearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        lock (_lock)
        {
            EnsureCatalogOpen();
            int limit = Math.Clamp(request.Limit ?? DefaultPageSize, 1, MaximumPageSize);
            int offset = DecodeCursor(request.Cursor);
            HashSet<string>? localeFilter = request.LocaleIds == null
                ? null
                : new HashSet<string>(request.LocaleIds, StringComparer.Ordinal);

            List<I18nEntry> matches = _catalog!.Entries
                .Where(entry => MatchesSearch(entry, request, localeFilter))
                .ToList();
            WorkspaceEntrySummaryResponse[] page = matches
                .Skip(offset)
                .Take(limit)
                .Select(entry => CreateEntrySummary(entry, request.IncludeLocalePreviews, localeFilter))
                .ToArray();
            int nextOffset = offset + page.Length;
            return new WorkspaceEntrySearchResponse(
                _revision,
                page,
                nextOffset < matches.Count ? EncodeCursor(nextOffset) : null);
        }
    }

    public WorkspaceEntriesResponse GetEntries(IReadOnlyCollection<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count > MaximumEntryBatchSize)
        {
            throw new WorkspaceException(
                WorkspaceErrorCodes.InvalidRequest,
                $"At most {MaximumEntryBatchSize.ToString(CultureInfo.InvariantCulture)} complete entries can be requested at once.");
        }

        lock (_lock)
        {
            EnsureCatalogOpen();
            var requested = new HashSet<string>(ids, StringComparer.Ordinal);
            CatalogEntryResponse[] entries = _catalog!.Entries
                .Where(entry => requested.Contains(entry.Id))
                .Select(CreateEntryResponse)
                .ToArray();
            var found = new HashSet<string>(entries.Select(entry => entry.Id), StringComparer.Ordinal);
            string[] missing = ids.Where(id => !found.Contains(id)).Distinct(StringComparer.Ordinal).ToArray();
            return new WorkspaceEntriesResponse(_revision, entries, missing);
        }
    }

    public WorkspaceWorkingChangesResponse GetWorkingChanges()
    {
        lock (_lock)
        {
            EnsureCatalogOpen();
            return new WorkspaceWorkingChangesResponse(
                _revision,
                HasChangesUnsafe(),
                _dirtyLocaleIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                _dirtyEntryIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                _dirtyPaths.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
                GetSourceStatusUnsafe());
        }
    }

    private bool MatchesSearch(
        I18nEntry entry,
        WorkspaceEntrySearchRequest request,
        HashSet<string>? localeFilter)
    {
        if (!string.IsNullOrWhiteSpace(request.PathPrefix) &&
            !entry.Path.StartsWith(request.PathPrefix.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            string query = request.Query.Trim();
            bool matched = entry.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                entry.Path.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (entry.Comment?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                entry.Locales.Any(pair =>
                    (localeFilter == null || localeFilter.Contains(pair.Key)) &&
                    (pair.Value.Text?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));
            if (!matched)
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.MissingLocale) &&
            HasLocaleValue(entry, request.MissingLocale))
        {
            return false;
        }

        IEnumerable<KeyValuePair<string, I18nLocaleValue>> values = localeFilter == null
            ? entry.Locales
            : entry.Locales.Where(pair => localeFilter.Contains(pair.Key));
        if (request.HasText is bool hasText && values.Any(pair => pair.Value.Text != null) != hasText)
        {
            return false;
        }

        if (request.HasAsset is bool hasAsset && values.Any(pair => pair.Value.Asset != null) != hasAsset)
        {
            return false;
        }

        return true;
    }

    private WorkspaceEntrySummaryResponse CreateEntrySummary(
        I18nEntry entry,
        bool includePreviews,
        HashSet<string>? localeFilter)
    {
        var previews = new Dictionary<string, WorkspaceTextPreviewResponse>(StringComparer.Ordinal);
        if (includePreviews)
        {
            foreach ((string localeId, I18nLocaleValue value) in entry.Locales)
            {
                if (value.Text == null || (localeFilter != null && !localeFilter.Contains(localeId)))
                {
                    continue;
                }

                bool truncated = value.Text.Length > TextPreviewLength;
                previews.Add(
                    localeId,
                    new WorkspaceTextPreviewResponse(
                        truncated ? value.Text[..TextPreviewLength] : value.Text,
                        truncated));
            }
        }

        string[] missingLocales = _catalog!.Locales
            .Where(locale => localeFilter == null || localeFilter.Contains(locale.Id))
            .Where(locale => !HasLocaleValue(entry, locale.Id))
            .Select(locale => locale.Id)
            .ToArray();
        return new WorkspaceEntrySummaryResponse(
            entry.Id,
            entry.Path,
            entry.Comment,
            previews,
            entry.Locales.Values.Any(value => value.Text != null),
            entry.Locales.Values.Any(value => value.Asset != null),
            missingLocales);
    }

    private static bool HasLocaleValue(I18nEntry entry, string localeId) =>
        entry.Locales.TryGetValue(localeId, out I18nLocaleValue? value) &&
        (value.Text != null || value.Asset != null);

    private string EncodeCursor(int offset)
    {
        string value = string.Create(CultureInfo.InvariantCulture, $"{_revision}:{offset}");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private int DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return 0;
        }

        try
        {
            string normalized = cursor.Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight(normalized.Length + ((4 - normalized.Length % 4) % 4), '=');
            string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(normalized));
            string[] parts = decoded.Split(':');
            if (parts.Length != 2 ||
                !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out long revision) ||
                !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int offset) ||
                offset < 0)
            {
                throw new FormatException();
            }

            if (revision != _revision)
            {
                throw new WorkspaceException(
                    WorkspaceErrorCodes.CatalogRevisionMismatch,
                    $"The cursor belongs to revision {revision}, but the workspace is at revision {_revision}. Start the search again.");
            }

            return offset;
        }
        catch (WorkspaceException)
        {
            throw;
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            throw new WorkspaceException(WorkspaceErrorCodes.InvalidCursor, "The search cursor is invalid.");
        }
    }

    private void EnsureCatalogOpen()
    {
        if (_catalog == null)
        {
            throw new WorkspaceException(WorkspaceErrorCodes.CatalogNotOpen, "No catalog is open in the workspace.");
        }
    }

    private CatalogSourceStatusResponse GetSourceStatusUnsafe()
    {
        if (_catalogPath == null || _baselineHash == null)
        {
            return new CatalogSourceStatusResponse(false, false, "No catalog is open in the workspace.");
        }

        try
        {
            byte[] sourceBytes = File.ReadAllBytes(_catalogPath);
            string sourceHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(sourceBytes));
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
