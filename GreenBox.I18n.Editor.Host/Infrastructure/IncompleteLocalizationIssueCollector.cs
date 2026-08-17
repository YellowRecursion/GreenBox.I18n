using GreenBox.I18n.Editor.Host.Contracts;
using GreenBox.I18n.Workspace.Contracts;

namespace GreenBox.I18n.Editor.Host.Infrastructure;

/// <summary>
/// Produces the same incomplete-localization warnings shown by the Web editor tree.
/// </summary>
internal static class IncompleteLocalizationIssueCollector
{
    internal static IEnumerable<WorkspaceIssueResponse> Enumerate(CatalogResponse catalog)
    {
        foreach (CatalogEntryResponse entry in catalog.Entries)
        {
            bool usesText = catalog.Locales.Any(locale =>
                TryGetValue(entry, locale.Id, out CatalogLocaleValueResponse? value) &&
                HasText(value?.Text));
            bool usesAsset = catalog.Locales.Any(locale =>
                TryGetValue(entry, locale.Id, out CatalogLocaleValueResponse? value) &&
                value?.Asset != null);

            if (!usesText && !usesAsset)
            {
                yield return new WorkspaceIssueResponse(
                    "incomplete",
                    "no_localized_content",
                    "warning",
                    $"Entry '{entry.Path}' has no localized content.",
                    entry.Id,
                    entry.Path);
                continue;
            }

            foreach (CatalogLocaleResponse locale in catalog.Locales)
            {
                TryGetValue(entry, locale.Id, out CatalogLocaleValueResponse? value);
                if (usesText && !HasText(value?.Text))
                {
                    yield return new WorkspaceIssueResponse(
                        "incomplete",
                        "missing_locale_text",
                        "warning",
                        $"Entry '{entry.Path}' has no text for locale '{locale.Id}'.",
                        entry.Id,
                        entry.Path,
                        locale.Id);
                }

                if (usesAsset && value?.Asset == null)
                {
                    yield return new WorkspaceIssueResponse(
                        "incomplete",
                        "missing_locale_asset",
                        "warning",
                        $"Entry '{entry.Path}' has no asset for locale '{locale.Id}'.",
                        entry.Id,
                        entry.Path,
                        locale.Id);
                }
            }
        }
    }

    private static bool TryGetValue(
        CatalogEntryResponse entry,
        string localeId,
        out CatalogLocaleValueResponse? value) =>
        entry.Locales.TryGetValue(localeId, out value);

    private static bool HasText(string? text) => !string.IsNullOrWhiteSpace(text);
}
