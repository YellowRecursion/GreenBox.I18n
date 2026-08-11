using System;
using System.Collections.Generic;

namespace GreenBox.I18n
{
    internal sealed class I18nCompiledCatalogBuilder
    {
        private readonly I18nStringPoolBuilder _strings = new I18nStringPoolBuilder();
        private readonly List<PendingLocale> _locales = new List<PendingLocale>();
        private readonly Dictionary<string, int> _localeIndexes =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<PendingEntry> _entries = new List<PendingEntry>();
        private readonly List<I18nCompiledMessageRecord> _messages = new List<I18nCompiledMessageRecord>();
        private readonly List<int> _messageArguments = new List<int>();
        private readonly List<I18nCompiledMessagePartRecord> _messageParts =
            new List<I18nCompiledMessagePartRecord>();
        private readonly List<I18nCompiledSelectorRecord> _selectors =
            new List<I18nCompiledSelectorRecord>();
        private readonly List<I18nCompiledVariantRecord> _variants =
            new List<I18nCompiledVariantRecord>();
        private readonly List<I18nCompiledVariantKeyRecord> _variantKeys =
            new List<I18nCompiledVariantKeyRecord>();
        private readonly string _defaultLocaleId;

        internal I18nCompiledCatalogBuilder(
            IReadOnlyList<I18nLocaleDefinition> locales,
            string defaultLocaleId)
        {
            _defaultLocaleId = defaultLocaleId;
            for (int index = 0; index < locales.Count; index++)
            {
                I18nLocaleDefinition locale = locales[index];
                _localeIndexes.Add(locale.Id, index);
                _locales.Add(new PendingLocale(locale));
            }
        }

        internal int AddMessage(I18nCompiledMessage message)
        {
            int index = _messages.Count;
            int firstArgument = _messageArguments.Count;
            for (int argumentIndex = 0; argumentIndex < message.ArgumentNames.Count; argumentIndex++)
            {
                _messageArguments.Add(_strings.Add(message.ArgumentNames[argumentIndex]));
            }

            int firstPart = AddParts(message.Parts);
            int firstSelector = _selectors.Count;
            int firstVariant = _variants.Count;
            I18nCompiledMessage.MessageMatcher? matcher = message.Matcher;
            if (matcher != null)
            {
                for (int selectorIndex = 0; selectorIndex < matcher.Selectors.Length; selectorIndex++)
                {
                    I18nCompiledMessage.MessageSelector selector = matcher.Selectors[selectorIndex];
                    _selectors.Add(new I18nCompiledSelectorRecord(
                        _strings.Add(selector.Name),
                        selector.Kind,
                        selector.NumberOptions));
                }

                for (int variantIndex = 0; variantIndex < matcher.Variants.Length; variantIndex++)
                {
                    I18nCompiledMessage.MessageVariant variant = matcher.Variants[variantIndex];
                    int firstKey = _variantKeys.Count;
                    for (int keyIndex = 0; keyIndex < variant.Keys.Length; keyIndex++)
                    {
                        I18nCompiledMessage.MessageVariantKey key = variant.Keys[keyIndex];
                        _variantKeys.Add(new I18nCompiledVariantKeyRecord(
                            key.Kind,
                            _strings.Add(key.Value),
                            key.ExactNumber));
                    }

                    int variantFirstPart = AddParts(variant.Parts);
                    _variants.Add(new I18nCompiledVariantRecord(
                        firstKey,
                        variant.Keys.Length,
                        variantFirstPart,
                        variant.Parts.Length));
                }
            }

            _messages.Add(new I18nCompiledMessageRecord(
                firstArgument,
                message.ArgumentNames.Count,
                firstPart,
                message.Parts.Length,
                firstSelector,
                matcher?.Selectors.Length ?? 0,
                firstVariant,
                matcher?.Variants.Length ?? 0));
            return index;
        }

        internal void AddEntry(long id, string path, List<PendingValue> values)
        {
            _entries.Add(new PendingEntry(id, path, values));
        }

        internal PendingValue CreateValue(
            string localeId,
            int message,
            I18nAssetReference? asset)
        {
            return new PendingValue(_localeIndexes[localeId], message, asset);
        }

        internal I18nCompiledCatalog Build()
        {
            _entries.Sort((left, right) => left.Id.CompareTo(right.Id));

            int defaultLocale = _localeIndexes[_defaultLocaleId];
            var fallbackLocales = new List<int>();
            var localeRecords = new I18nCompiledLocaleRecord[_locales.Count];
            for (int index = 0; index < _locales.Count; index++)
            {
                PendingLocale locale = _locales[index];
                int firstFallback = fallbackLocales.Count;
                AppendFallbackChain(index, defaultLocale, fallbackLocales);
                localeRecords[index] = new I18nCompiledLocaleRecord(
                    _strings.Add(locale.Id),
                    _strings.Add(locale.DisplayName),
                    _strings.Add(locale.CultureName),
                    locale.FallbackId == null
                        ? I18nCompiledCatalogFormat.MissingIndex
                        : _localeIndexes[locale.FallbackId],
                    firstFallback,
                    fallbackLocales.Count - firstFallback,
                    AddAsset(locale.Icon));
            }

            var values = new List<I18nCompiledValueRecord>();
            var entries = new I18nCompiledEntryRecord[_entries.Count];
            for (int entryIndex = 0; entryIndex < _entries.Count; entryIndex++)
            {
                PendingEntry entry = _entries[entryIndex];
                entry.Values.Sort((left, right) => left.Locale.CompareTo(right.Locale));
                int firstValue = values.Count;
                for (int valueIndex = 0; valueIndex < entry.Values.Count; valueIndex++)
                {
                    PendingValue value = entry.Values[valueIndex];
                    values.Add(new I18nCompiledValueRecord(
                        value.Locale,
                        value.Message,
                        AddAsset(value.Asset)));
                }

                entries[entryIndex] = new I18nCompiledEntryRecord(
                    entry.Id,
                    _strings.Add(entry.Path),
                    firstValue,
                    values.Count - firstValue);
            }

            return new I18nCompiledCatalog(
                new I18nCompiledCatalogStorage(
                    defaultLocale,
                    _strings.ToArray(),
                    localeRecords,
                    fallbackLocales.ToArray(),
                    entries,
                    values.ToArray(),
                    _messages.ToArray(),
                    _messageArguments.ToArray(),
                    _messageParts.ToArray(),
                    _selectors.ToArray(),
                    _variants.ToArray(),
                    _variantKeys.ToArray()));
        }

        private int AddParts(I18nCompiledMessage.MessagePart[] parts)
        {
            int first = _messageParts.Count;
            for (int index = 0; index < parts.Length; index++)
            {
                I18nCompiledMessage.MessagePart part = parts[index];
                _messageParts.Add(new I18nCompiledMessagePartRecord(
                    part.Kind,
                    _strings.Add(part.Value),
                    part.SourcePosition,
                    part.NumberOptions));
            }

            return first;
        }

        private void AppendFallbackChain(int locale, int defaultLocale, List<int> result)
        {
            int first = result.Count;
            int current = locale;
            while (true)
            {
                result.Add(current);
                string? fallbackId = _locales[current].FallbackId;
                if (fallbackId == null)
                {
                    break;
                }

                current = _localeIndexes[fallbackId];
            }

            if (!Contains(result, first, defaultLocale))
            {
                result.Add(defaultLocale);
            }
        }

        private static bool Contains(List<int> values, int first, int value)
        {
            for (int index = first; index < values.Count; index++)
            {
                if (values[index] == value)
                {
                    return true;
                }
            }

            return false;
        }

        private I18nCompiledAssetRecord AddAsset(I18nAssetReference? asset)
        {
            return asset == null
                ? I18nCompiledAssetRecord.Empty
                : new I18nCompiledAssetRecord(
                    _strings.Add(asset.AssetGuid),
                    asset.LocalFileId == null
                        ? I18nCompiledCatalogFormat.MissingIndex
                        : _strings.Add(asset.LocalFileId));
        }

        internal readonly struct PendingValue
        {
            internal PendingValue(int locale, int message, I18nAssetReference? asset)
            {
                Locale = locale;
                Message = message;
                Asset = asset;
            }

            internal int Locale { get; }
            internal int Message { get; }
            internal I18nAssetReference? Asset { get; }
        }

        private sealed class PendingEntry
        {
            internal PendingEntry(long id, string path, List<PendingValue> values)
            {
                Id = id;
                Path = path;
                Values = values;
            }

            internal long Id { get; }
            internal string Path { get; }
            internal List<PendingValue> Values { get; }
        }

        private sealed class PendingLocale
        {
            internal PendingLocale(I18nLocaleDefinition locale)
            {
                Id = locale.Id;
                DisplayName = locale.DisplayName;
                CultureName = locale.Culture;
                FallbackId = locale.Fallback;
                Icon = locale.Icon;
            }

            internal string Id { get; }
            internal string DisplayName { get; }
            internal string CultureName { get; }
            internal string? FallbackId { get; }
            internal I18nAssetReference? Icon { get; }
        }
    }

    internal sealed class I18nStringPoolBuilder
    {
        private readonly Dictionary<string, int> _indexes =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<string> _values = new List<string>();

        internal int Add(string value)
        {
            if (_indexes.TryGetValue(value, out int index))
            {
                return index;
            }

            index = _values.Count;
            _values.Add(value);
            _indexes.Add(value, index);
            return index;
        }

        internal string[] ToArray()
        {
            return _values.ToArray();
        }
    }
}
