using System;
using System.Collections.Generic;
using System.Linq;

namespace GreenBox.I18n
{
    /// <summary>
    /// Performs a safe three-way merge of localization catalogs.
    /// </summary>
    public static class I18nCatalogMerge
    {
        /// <summary>
        /// Merges a working copy and an incoming catalog relative to their common baseline.
        /// </summary>
        /// <param name="baseline">The catalog version both sides started from.</param>
        /// <param name="current">The current local working copy.</param>
        /// <param name="incoming">The newly loaded catalog from an external source.</param>
        /// <returns>The merged catalog, or conflicts when a safe result cannot be produced.</returns>
        public static I18nCatalogMergeResult Merge(
            I18nCatalog baseline,
            I18nCatalog current,
            I18nCatalog incoming)
        {
            if (baseline == null) throw new ArgumentNullException(nameof(baseline));
            if (current == null) throw new ArgumentNullException(nameof(current));
            if (incoming == null) throw new ArgumentNullException(nameof(incoming));

            var conflicts = new List<I18nCatalogMergeConflict>();
            AddInputValidationConflicts(baseline, "Baseline", conflicts);
            AddInputValidationConflicts(current, "Current", conflicts);
            AddInputValidationConflicts(incoming, "Incoming", conflicts);
            if (conflicts.Count > 0)
            {
                return I18nCatalogMergeResult.Failure(conflicts);
            }

            var merged = new I18nCatalog
            {
                FileComment = MergeValue(
                    baseline.FileComment,
                    current.FileComment,
                    incoming.FileComment,
                    "$.$comment",
                    conflicts),
                SchemaVersion = MergeValue(
                    baseline.SchemaVersion,
                    current.SchemaVersion,
                    incoming.SchemaVersion,
                    "$.schemaVersion",
                    conflicts),
                DefaultLocale = MergeValue(
                    baseline.DefaultLocale,
                    current.DefaultLocale,
                    incoming.DefaultLocale,
                    "$.defaultLocale",
                    conflicts),
            };

            merged.Locales = MergeLocales(baseline.Locales, current.Locales, incoming.Locales, conflicts);
            merged.Entries = MergeEntries(baseline.Entries, current.Entries, incoming.Entries, conflicts);

            I18nValidationResult validation = I18nCatalogValidator.Validate(merged);
            foreach (I18nValidationDiagnostic diagnostic in validation.Diagnostics.Where(
                         diagnostic => diagnostic.Severity == I18nValidationSeverity.Error))
            {
                conflicts.Add(new I18nCatalogMergeConflict(diagnostic.JsonPath, diagnostic.Message));
            }

            return conflicts.Count == 0
                ? I18nCatalogMergeResult.Success(merged)
                : I18nCatalogMergeResult.Failure(conflicts);
        }

        private static List<I18nLocaleDefinition> MergeLocales(
            IReadOnlyList<I18nLocaleDefinition> baseline,
            IReadOnlyList<I18nLocaleDefinition> current,
            IReadOnlyList<I18nLocaleDefinition> incoming,
            List<I18nCatalogMergeConflict> conflicts)
        {
            Dictionary<string, I18nLocaleDefinition> baselineById = baseline.ToDictionary(locale => locale.Id, StringComparer.Ordinal);
            Dictionary<string, I18nLocaleDefinition> currentById = current.ToDictionary(locale => locale.Id, StringComparer.Ordinal);
            Dictionary<string, I18nLocaleDefinition> incomingById = incoming.ToDictionary(locale => locale.Id, StringComparer.Ordinal);
            var mergedById = new Dictionary<string, I18nLocaleDefinition>(StringComparer.Ordinal);

            foreach (string id in AllKeys(baselineById, currentById, incomingById))
            {
                I18nLocaleDefinition? locale = MergeNode(
                    baselineById.TryGetValue(id, out I18nLocaleDefinition? baselineLocale) ? baselineLocale : null,
                    currentById.TryGetValue(id, out I18nLocaleDefinition? currentLocale) ? currentLocale : null,
                    incomingById.TryGetValue(id, out I18nLocaleDefinition? incomingLocale) ? incomingLocale : null,
                    locale => CloneLocale(locale),
                    LocaleEquals,
                    (baselineValue, currentValue, incomingValue) => MergeLocale(
                        baselineValue,
                        currentValue,
                        incomingValue,
                        conflicts),
                    $"$.locales[id={id}]",
                    conflicts);
                if (locale != null)
                {
                    mergedById.Add(id, locale);
                }
            }

            IReadOnlyList<string> order = MergeOrder(
                baseline.Select(locale => locale.Id),
                current.Select(locale => locale.Id),
                incoming.Select(locale => locale.Id),
                mergedById.Keys,
                "$.locales",
                conflicts);
            return order.Select(id => mergedById[id]).ToList();
        }

        private static I18nLocaleDefinition MergeLocale(
            I18nLocaleDefinition baseline,
            I18nLocaleDefinition current,
            I18nLocaleDefinition incoming,
            List<I18nCatalogMergeConflict> conflicts)
        {
            string path = $"$.locales[id={baseline.Id}]";
            return new I18nLocaleDefinition
            {
                Id = baseline.Id,
                DisplayName = MergeValue(baseline.DisplayName, current.DisplayName, incoming.DisplayName, $"{path}.displayName", conflicts),
                Culture = MergeValue(baseline.Culture, current.Culture, incoming.Culture, $"{path}.culture", conflicts),
                Fallback = MergeValue(baseline.Fallback, current.Fallback, incoming.Fallback, $"{path}.fallback", conflicts),
                Icon = MergeAsset(baseline.Icon, current.Icon, incoming.Icon, $"{path}.icon", conflicts),
            };
        }

        private static List<I18nEntry> MergeEntries(
            IReadOnlyList<I18nEntry> baseline,
            IReadOnlyList<I18nEntry> current,
            IReadOnlyList<I18nEntry> incoming,
            List<I18nCatalogMergeConflict> conflicts)
        {
            Dictionary<string, I18nEntry> baselineById = baseline.ToDictionary(entry => entry.Id, StringComparer.Ordinal);
            Dictionary<string, I18nEntry> currentById = current.ToDictionary(entry => entry.Id, StringComparer.Ordinal);
            Dictionary<string, I18nEntry> incomingById = incoming.ToDictionary(entry => entry.Id, StringComparer.Ordinal);
            var merged = new List<I18nEntry>();

            foreach (string id in AllKeys(baselineById, currentById, incomingById))
            {
                I18nEntry? entry = MergeNode(
                    baselineById.TryGetValue(id, out I18nEntry? baselineEntry) ? baselineEntry : null,
                    currentById.TryGetValue(id, out I18nEntry? currentEntry) ? currentEntry : null,
                    incomingById.TryGetValue(id, out I18nEntry? incomingEntry) ? incomingEntry : null,
                    CloneEntry,
                    EntryEquals,
                    (baselineValue, currentValue, incomingValue) => MergeEntry(
                        baselineValue,
                        currentValue,
                        incomingValue,
                        conflicts),
                    $"$.entries[id={id}]",
                    conflicts);
                if (entry != null)
                {
                    merged.Add(entry);
                }
            }

            merged.Sort(I18nEntryComparer.Canonical);
            return merged;
        }

        private static I18nEntry MergeEntry(
            I18nEntry baseline,
            I18nEntry current,
            I18nEntry incoming,
            List<I18nCatalogMergeConflict> conflicts)
        {
            string path = $"$.entries[id={baseline.Id}]";
            return new I18nEntry
            {
                Id = baseline.Id,
                Path = MergeValue(baseline.Path, current.Path, incoming.Path, $"{path}.path", conflicts),
                Comment = MergeValue(baseline.Comment, current.Comment, incoming.Comment, $"{path}.comment", conflicts),
                Locales = MergeLocaleValues(baseline.Locales, current.Locales, incoming.Locales, path, conflicts),
            };
        }

        private static Dictionary<string, I18nLocaleValue> MergeLocaleValues(
            IReadOnlyDictionary<string, I18nLocaleValue> baseline,
            IReadOnlyDictionary<string, I18nLocaleValue> current,
            IReadOnlyDictionary<string, I18nLocaleValue> incoming,
            string entryPath,
            List<I18nCatalogMergeConflict> conflicts)
        {
            var merged = new Dictionary<string, I18nLocaleValue>(StringComparer.Ordinal);
            foreach (string localeId in AllKeys(baseline, current, incoming))
            {
                I18nLocaleValue? value = MergeNode(
                    baseline.TryGetValue(localeId, out I18nLocaleValue? baselineValue) ? baselineValue : null,
                    current.TryGetValue(localeId, out I18nLocaleValue? currentValue) ? currentValue : null,
                    incoming.TryGetValue(localeId, out I18nLocaleValue? incomingValue) ? incomingValue : null,
                    CloneLocaleValue,
                    LocaleValueEquals,
                    (baselineItem, currentItem, incomingItem) => new I18nLocaleValue
                    {
                        Text = MergeValue(
                            baselineItem.Text,
                            currentItem.Text,
                            incomingItem.Text,
                            $"{entryPath}.locales.{localeId}.text",
                            conflicts),
                        Asset = MergeAsset(
                            baselineItem.Asset,
                            currentItem.Asset,
                            incomingItem.Asset,
                            $"{entryPath}.locales.{localeId}.asset",
                            conflicts),
                    },
                    $"{entryPath}.locales.{localeId}",
                    conflicts);
                if (value != null)
                {
                    merged.Add(localeId, value);
                }
            }

            return merged;
        }

        private static I18nAssetReference? MergeAsset(
            I18nAssetReference? baseline,
            I18nAssetReference? current,
            I18nAssetReference? incoming,
            string path,
            List<I18nCatalogMergeConflict> conflicts)
        {
            return MergeNode(
                baseline,
                current,
                incoming,
                CloneAsset,
                AssetEquals,
                (baselineAsset, currentAsset, incomingAsset) => new I18nAssetReference
                {
                    AssetGuid = MergeValue(
                        baselineAsset.AssetGuid,
                        currentAsset.AssetGuid,
                        incomingAsset.AssetGuid,
                        $"{path}.assetGuid",
                        conflicts),
                    LocalFileId = MergeValue(
                        baselineAsset.LocalFileId,
                        currentAsset.LocalFileId,
                        incomingAsset.LocalFileId,
                        $"{path}.localFileId",
                        conflicts),
                },
                path,
                conflicts);
        }

        private static T? MergeNode<T>(
            T? baseline,
            T? current,
            T? incoming,
            Func<T, T> clone,
            Func<T?, T?, bool> equals,
            Func<T, T, T, T> mergeExisting,
            string path,
            List<I18nCatalogMergeConflict> conflicts)
            where T : class
        {
            if (equals(current, incoming)) return current == null ? null : clone(current);
            if (equals(current, baseline)) return incoming == null ? null : clone(incoming);
            if (equals(incoming, baseline)) return current == null ? null : clone(current);
            if (baseline != null && current != null && incoming != null)
            {
                return mergeExisting(baseline, current, incoming);
            }

            conflicts.Add(new I18nCatalogMergeConflict(
                path,
                baseline == null ? "Both sides added different values." : "Deletion conflicts with an edit."));
            return current == null ? null : clone(current);
        }

        private static T MergeValue<T>(
            T baseline,
            T current,
            T incoming,
            string path,
            List<I18nCatalogMergeConflict> conflicts)
        {
            var comparer = EqualityComparer<T>.Default;
            if (comparer.Equals(current, incoming)) return current;
            if (comparer.Equals(current, baseline)) return incoming;
            if (comparer.Equals(incoming, baseline)) return current;

            conflicts.Add(new I18nCatalogMergeConflict(path, "Both sides changed this value differently."));
            return current;
        }

        private static IReadOnlyList<string> MergeOrder(
            IEnumerable<string> baseline,
            IEnumerable<string> current,
            IEnumerable<string> incoming,
            IEnumerable<string> mergedIds,
            string path,
            List<I18nCatalogMergeConflict> conflicts)
        {
            var included = new HashSet<string>(mergedIds, StringComparer.Ordinal);
            List<string> baselineOrder = baseline.Where(included.Contains).ToList();
            List<string> currentOrder = current.Where(included.Contains).ToList();
            List<string> incomingOrder = incoming.Where(included.Contains).ToList();
            if (currentOrder.SequenceEqual(incomingOrder)) return currentOrder;
            if (currentOrder.SequenceEqual(baselineOrder)) return incomingOrder;
            if (incomingOrder.SequenceEqual(baselineOrder)) return currentOrder;

            IReadOnlyList<string>? combinedOrder = TryCombineOrders(
                baselineOrder,
                currentOrder,
                incomingOrder,
                included);
            if (combinedOrder != null)
            {
                return combinedOrder;
            }

            conflicts.Add(new I18nCatalogMergeConflict(path, "Both sides reordered locales incompatibly."));
            return currentOrder.Concat(incomingOrder).Distinct(StringComparer.Ordinal).ToList();
        }

        private static IReadOnlyList<string>? TryCombineOrders(
            IReadOnlyList<string> baseline,
            IReadOnlyList<string> current,
            IReadOnlyList<string> incoming,
            IReadOnlyCollection<string> included)
        {
            var outgoing = included.ToDictionary(
                id => id,
                _ => new HashSet<string>(StringComparer.Ordinal),
                StringComparer.Ordinal);
            var incomingEdgeCount = included.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);

            AddOrderEdges(current, outgoing, incomingEdgeCount);
            AddOrderEdges(incoming, outgoing, incomingEdgeCount);

            var baselineIndex = baseline
                .Select((id, index) => new { id, index })
                .ToDictionary(pair => pair.id, pair => pair.index, StringComparer.Ordinal);
            var available = new SortedSet<string>(Comparer<string>.Create((left, right) =>
            {
                if (ReferenceEquals(left, right)) return 0;
                int leftIndex = baselineIndex.TryGetValue(left, out int value) ? value : int.MaxValue;
                int rightIndex = baselineIndex.TryGetValue(right, out value) ? value : int.MaxValue;
                int byBaseline = leftIndex.CompareTo(rightIndex);
                return byBaseline != 0 ? byBaseline : StringComparer.Ordinal.Compare(left, right);
            }));

            foreach (string id in included)
            {
                if (incomingEdgeCount[id] == 0)
                {
                    available.Add(id);
                }
            }

            var result = new List<string>(included.Count);
            while (available.Count > 0)
            {
                string id = available.Min!;
                available.Remove(id);
                result.Add(id);

                foreach (string next in outgoing[id])
                {
                    incomingEdgeCount[next]--;
                    if (incomingEdgeCount[next] == 0)
                    {
                        available.Add(next);
                    }
                }
            }

            return result.Count == included.Count ? result : null;
        }

        private static void AddOrderEdges(
            IReadOnlyList<string> order,
            IReadOnlyDictionary<string, HashSet<string>> outgoing,
            IDictionary<string, int> incomingEdgeCount)
        {
            for (int index = 1; index < order.Count; index++)
            {
                string previous = order[index - 1];
                string next = order[index];
                if (outgoing[previous].Add(next))
                {
                    incomingEdgeCount[next]++;
                }
            }
        }

        private static void AddInputValidationConflicts(
            I18nCatalog catalog,
            string inputName,
            ICollection<I18nCatalogMergeConflict> conflicts)
        {
            I18nValidationResult validation = I18nCatalogValidator.Validate(catalog);
            foreach (I18nValidationDiagnostic diagnostic in validation.Diagnostics.Where(
                         diagnostic => diagnostic.Severity == I18nValidationSeverity.Error))
            {
                conflicts.Add(new I18nCatalogMergeConflict(
                    diagnostic.JsonPath,
                    $"{inputName} catalog is invalid: {diagnostic.Message}"));
            }
        }

        private static IEnumerable<string> AllKeys<T>(
            IReadOnlyDictionary<string, T> baseline,
            IReadOnlyDictionary<string, T> current,
            IReadOnlyDictionary<string, T> incoming)
        {
            return baseline.Keys.Concat(current.Keys).Concat(incoming.Keys).Distinct(StringComparer.Ordinal);
        }

        private static bool LocaleEquals(I18nLocaleDefinition? left, I18nLocaleDefinition? right)
        {
            return ReferenceEquals(left, right) || left != null && right != null &&
                left.Id == right.Id && left.DisplayName == right.DisplayName && left.Culture == right.Culture &&
                left.Fallback == right.Fallback && AssetEquals(left.Icon, right.Icon);
        }

        private static bool EntryEquals(I18nEntry? left, I18nEntry? right)
        {
            return ReferenceEquals(left, right) || left != null && right != null &&
                left.Id == right.Id && left.Path == right.Path && left.Comment == right.Comment &&
                DictionaryEquals(left.Locales, right.Locales, LocaleValueEquals);
        }

        private static bool LocaleValueEquals(I18nLocaleValue? left, I18nLocaleValue? right)
        {
            return ReferenceEquals(left, right) || left != null && right != null &&
                left.Text == right.Text && AssetEquals(left.Asset, right.Asset);
        }

        private static bool AssetEquals(I18nAssetReference? left, I18nAssetReference? right)
        {
            return ReferenceEquals(left, right) || left != null && right != null &&
                left.AssetGuid == right.AssetGuid && left.LocalFileId == right.LocalFileId;
        }

        private static bool DictionaryEquals<T>(
            IReadOnlyDictionary<string, T> left,
            IReadOnlyDictionary<string, T> right,
            Func<T?, T?, bool> valueEquals)
            where T : class
        {
            return left.Count == right.Count && left.All(pair =>
                right.TryGetValue(pair.Key, out T? rightValue) && valueEquals(pair.Value, rightValue));
        }

        private static I18nLocaleDefinition CloneLocale(I18nLocaleDefinition locale)
        {
            return new I18nLocaleDefinition
            {
                Id = locale.Id,
                DisplayName = locale.DisplayName,
                Culture = locale.Culture,
                Fallback = locale.Fallback,
                Icon = locale.Icon == null ? null : CloneAsset(locale.Icon),
            };
        }

        private static I18nEntry CloneEntry(I18nEntry entry)
        {
            return new I18nEntry
            {
                Id = entry.Id,
                Path = entry.Path,
                Comment = entry.Comment,
                Locales = entry.Locales.ToDictionary(
                    pair => pair.Key,
                    pair => CloneLocaleValue(pair.Value),
                    StringComparer.Ordinal),
            };
        }

        private static I18nLocaleValue CloneLocaleValue(I18nLocaleValue value)
        {
            return new I18nLocaleValue
            {
                Text = value.Text,
                Asset = value.Asset == null ? null : CloneAsset(value.Asset),
            };
        }

        private static I18nAssetReference CloneAsset(I18nAssetReference asset)
        {
            return new I18nAssetReference
            {
                AssetGuid = asset.AssetGuid,
                LocalFileId = asset.LocalFileId,
            };
        }
    }

    /// <summary>
    /// Contains the outcome of a three-way catalog merge.
    /// </summary>
    public sealed class I18nCatalogMergeResult
    {
        private I18nCatalogMergeResult(I18nCatalog? catalog, IReadOnlyList<I18nCatalogMergeConflict> conflicts)
        {
            Catalog = catalog;
            Conflicts = conflicts;
        }

        /// <summary>
        /// Gets the safely merged catalog, or <see langword="null"/> when conflicts exist.
        /// </summary>
        public I18nCatalog? Catalog { get; }

        /// <summary>
        /// Gets conflicts that prevented a safe merge.
        /// </summary>
        public IReadOnlyList<I18nCatalogMergeConflict> Conflicts { get; }

        /// <summary>
        /// Gets whether the merge completed without conflicts.
        /// </summary>
        public bool IsSuccess => Catalog != null;

        internal static I18nCatalogMergeResult Success(I18nCatalog catalog)
        {
            return new I18nCatalogMergeResult(catalog, Array.Empty<I18nCatalogMergeConflict>());
        }

        internal static I18nCatalogMergeResult Failure(IReadOnlyList<I18nCatalogMergeConflict> conflicts)
        {
            return new I18nCatalogMergeResult(null, conflicts);
        }
    }

    /// <summary>
    /// Describes one field that could not be merged safely.
    /// </summary>
    public sealed class I18nCatalogMergeConflict
    {
        /// <summary>
        /// Creates a catalog merge conflict.
        /// </summary>
        /// <param name="jsonPath">The logical JSON path of the conflicting value.</param>
        /// <param name="message">The human-readable conflict description.</param>
        public I18nCatalogMergeConflict(string jsonPath, string message)
        {
            JsonPath = jsonPath;
            Message = message;
        }

        /// <summary>
        /// Gets the logical JSON path of the conflicting value.
        /// </summary>
        public string JsonPath { get; }

        /// <summary>
        /// Gets the human-readable conflict description.
        /// </summary>
        public string Message { get; }
    }
}
