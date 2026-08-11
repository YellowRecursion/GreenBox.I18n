using System;
using System.IO;

namespace GreenBox.I18n
{
    internal static class I18nCompiledCatalogFormat
    {
        internal const uint Magic = 0x31494247; // GBI1
        internal const int BinaryVersion = 3;
        internal const int CompilerVersion = 2;
        internal const int MaximumCollectionSize = 10_000_000;
        internal const int MissingIndex = -1;
    }

    internal sealed class I18nCompiledCatalogStorage
    {
        internal I18nCompiledCatalogStorage(
            int defaultLocale,
            string[] strings,
            I18nCompiledLocaleRecord[] locales,
            int[] fallbackLocales,
            I18nCompiledEntryRecord[] entries,
            I18nCompiledValueRecord[] values,
            I18nCompiledMessageRecord[] messages,
            int[] messageArguments,
            I18nCompiledMessage.NumberOptions[] numberOptions,
            I18nCompiledMessagePartRecord[] messageParts,
            I18nCompiledSelectorRecord[] selectors,
            I18nCompiledVariantRecord[] variants,
            I18nCompiledVariantKeyRecord[] variantKeys)
        {
            DefaultLocale = defaultLocale;
            Strings = strings ?? throw new ArgumentNullException(nameof(strings));
            Locales = locales ?? throw new ArgumentNullException(nameof(locales));
            FallbackLocales = fallbackLocales ?? throw new ArgumentNullException(nameof(fallbackLocales));
            Entries = entries ?? throw new ArgumentNullException(nameof(entries));
            Values = values ?? throw new ArgumentNullException(nameof(values));
            Messages = messages ?? throw new ArgumentNullException(nameof(messages));
            MessageArguments = messageArguments ?? throw new ArgumentNullException(nameof(messageArguments));
            NumberOptions = numberOptions ?? throw new ArgumentNullException(nameof(numberOptions));
            MessageParts = messageParts ?? throw new ArgumentNullException(nameof(messageParts));
            Selectors = selectors ?? throw new ArgumentNullException(nameof(selectors));
            Variants = variants ?? throw new ArgumentNullException(nameof(variants));
            VariantKeys = variantKeys ?? throw new ArgumentNullException(nameof(variantKeys));

            Validate();
        }

        internal int DefaultLocale { get; }
        internal string[] Strings { get; }
        internal I18nCompiledLocaleRecord[] Locales { get; }
        internal int[] FallbackLocales { get; }
        internal I18nCompiledEntryRecord[] Entries { get; }
        internal I18nCompiledValueRecord[] Values { get; }
        internal I18nCompiledMessageRecord[] Messages { get; }
        internal int[] MessageArguments { get; }
        internal I18nCompiledMessage.NumberOptions[] NumberOptions { get; }
        internal I18nCompiledMessagePartRecord[] MessageParts { get; }
        internal I18nCompiledSelectorRecord[] Selectors { get; }
        internal I18nCompiledVariantRecord[] Variants { get; }
        internal I18nCompiledVariantKeyRecord[] VariantKeys { get; }

        internal string GetString(int index)
        {
            return Strings[index];
        }

        internal I18nAssetReference? CreateAsset(I18nCompiledAssetRecord asset)
        {
            return !asset.HasValue
                ? null
                : new I18nAssetReference
                {
                    AssetGuid = GetString(asset.AssetGuid),
                    LocalFileId = asset.LocalFileId == I18nCompiledCatalogFormat.MissingIndex
                        ? null
                        : GetString(asset.LocalFileId),
                };
        }

        private void Validate()
        {
            if (Locales.Length == 0 || DefaultLocale < 0 || DefaultLocale >= Locales.Length)
            {
                throw Invalid("default locale index");
            }

            for (int index = 0; index < Strings.Length; index++)
            {
                if (Strings[index] == null)
                {
                    throw Invalid("string table");
                }
            }

            int nextFallback = 0;
            for (int index = 0; index < Locales.Length; index++)
            {
                I18nCompiledLocaleRecord locale = Locales[index];
                ValidateString(locale.Id);
                ValidateString(locale.DisplayName);
                ValidateString(locale.CultureName);
                ValidateOptionalIndex(locale.FallbackLocale, Locales.Length, "locale fallback");
                ValidateRange(locale.FirstFallback, locale.FallbackCount, FallbackLocales.Length, "fallback range");
                if (locale.FirstFallback != nextFallback ||
                    locale.FallbackCount == 0 ||
                    FallbackLocales[locale.FirstFallback] != index)
                {
                    throw Invalid("fallback chain");
                }

                nextFallback += locale.FallbackCount;

                bool containsDefault = false;
                for (int fallbackIndex = locale.FirstFallback;
                     fallbackIndex < locale.FirstFallback + locale.FallbackCount;
                     fallbackIndex++)
                {
                    int fallbackLocale = FallbackLocales[fallbackIndex];
                    ValidateIndex(fallbackLocale, Locales.Length, "fallback locale");
                    containsDefault |= fallbackLocale == DefaultLocale;
                    for (int previous = locale.FirstFallback; previous < fallbackIndex; previous++)
                    {
                        if (FallbackLocales[previous] == fallbackLocale)
                        {
                            throw Invalid("fallback chain");
                        }
                    }
                }

                if (!containsDefault)
                {
                    throw Invalid("fallback chain");
                }

                ValidateAsset(locale.Icon);
            }

            if (nextFallback != FallbackLocales.Length)
            {
                throw Invalid("fallback layout");
            }

            for (int index = 0; index < FallbackLocales.Length; index++)
            {
                ValidateIndex(FallbackLocales[index], Locales.Length, "fallback locale");
            }

            long previousId = long.MinValue;
            int nextValue = 0;
            for (int index = 0; index < Entries.Length; index++)
            {
                I18nCompiledEntryRecord entry = Entries[index];
                if (!I18nEntryId.IsValid(entry.Id) || (index > 0 && entry.Id <= previousId))
                {
                    throw Invalid("entry ordering");
                }

                previousId = entry.Id;
                ValidateString(entry.Path);
                ValidateRange(entry.FirstValue, entry.ValueCount, Values.Length, "entry value range");
                if (entry.FirstValue != nextValue)
                {
                    throw Invalid("entry value layout");
                }

                nextValue += entry.ValueCount;

                int previousLocale = -1;
                for (int valueIndex = entry.FirstValue;
                     valueIndex < entry.FirstValue + entry.ValueCount;
                     valueIndex++)
                {
                    I18nCompiledValueRecord value = Values[valueIndex];
                    ValidateIndex(value.Locale, Locales.Length, "value locale");
                    if (value.Locale <= previousLocale)
                    {
                        throw Invalid("value locale ordering");
                    }

                    previousLocale = value.Locale;
                    ValidateOptionalIndex(value.Message, Messages.Length, "value message");
                    ValidateAsset(value.Asset);
                }
            }

            if (nextValue != Values.Length)
            {
                throw Invalid("entry value layout");
            }

            for (int index = 0; index < MessageArguments.Length; index++)
            {
                ValidateString(MessageArguments[index]);
            }

            for (int index = 0; index < NumberOptions.Length; index++)
            {
                if (!NumberOptions[index].IsValid)
                {
                    throw Invalid("number options");
                }
            }

            for (int index = 0; index < MessageParts.Length; index++)
            {
                I18nCompiledMessagePartRecord part = MessageParts[index];
                ValidateString(part.Value);
                if ((int)part.Kind < (int)I18nCompiledMessage.MessagePartKind.Text ||
                    (int)part.Kind > (int)I18nCompiledMessage.MessagePartKind.NumberVariable)
                {
                    throw Invalid("message part kind");
                }

                if (part.Kind == I18nCompiledMessage.MessagePartKind.NumberVariable)
                {
                    ValidateIndex(part.NumberOptions, NumberOptions.Length, "part number options");
                }
                else if (part.NumberOptions != I18nCompiledCatalogFormat.MissingIndex)
                {
                    throw Invalid("part number options");
                }
            }

            for (int index = 0; index < Selectors.Length; index++)
            {
                I18nCompiledSelectorRecord selector = Selectors[index];
                ValidateString(selector.Name);
                if ((int)selector.Kind < (int)I18nCompiledMessage.MessageSelectorKind.CardinalNumber ||
                    (int)selector.Kind > (int)I18nCompiledMessage.MessageSelectorKind.String)
                {
                    throw Invalid("selector kind");
                }

                if (selector.Kind == I18nCompiledMessage.MessageSelectorKind.String)
                {
                    if (selector.NumberOptions != I18nCompiledCatalogFormat.MissingIndex)
                    {
                        throw Invalid("selector number options");
                    }
                }
                else
                {
                    ValidateIndex(
                        selector.NumberOptions,
                        NumberOptions.Length,
                        "selector number options");
                }
            }

            for (int index = 0; index < VariantKeys.Length; index++)
            {
                I18nCompiledVariantKeyRecord key = VariantKeys[index];
                ValidateString(key.Value);
                if ((int)key.Kind < (int)I18nCompiledMessage.MessageVariantKeyKind.Wildcard ||
                    (int)key.Kind > (int)I18nCompiledMessage.MessageVariantKeyKind.String)
                {
                    throw Invalid("variant key kind");
                }
            }

            int nextArgument = 0;
            int nextPart = 0;
            int nextSelector = 0;
            int nextVariant = 0;
            int nextKey = 0;
            for (int index = 0; index < Messages.Length; index++)
            {
                I18nCompiledMessageRecord message = Messages[index];
                ValidateRange(message.FirstArgument, message.ArgumentCount, MessageArguments.Length, "message argument range");
                ValidateRange(message.FirstPart, message.PartCount, MessageParts.Length, "message part range");
                ValidateRange(message.FirstSelector, message.SelectorCount, Selectors.Length, "message selector range");
                ValidateRange(message.FirstVariant, message.VariantCount, Variants.Length, "message variant range");
                if (message.FirstArgument != nextArgument ||
                    message.FirstPart != nextPart ||
                    message.FirstSelector != nextSelector ||
                    message.FirstVariant != nextVariant)
                {
                    throw Invalid("message layout");
                }

                nextArgument += message.ArgumentCount;
                nextPart += message.PartCount;
                nextSelector += message.SelectorCount;
                nextVariant += message.VariantCount;

                if ((message.SelectorCount == 0) != (message.VariantCount == 0))
                {
                    throw Invalid("message matcher layout");
                }

                for (int variantOffset = 0; variantOffset < message.VariantCount; variantOffset++)
                {
                    I18nCompiledVariantRecord variant = Variants[message.FirstVariant + variantOffset];
                    ValidateRange(variant.FirstKey, variant.KeyCount, VariantKeys.Length, "variant key range");
                    ValidateRange(variant.FirstPart, variant.PartCount, MessageParts.Length, "variant part range");
                    if (variant.FirstKey != nextKey ||
                        variant.FirstPart != nextPart ||
                        variant.KeyCount != message.SelectorCount)
                    {
                        throw Invalid("variant layout");
                    }

                    nextKey += variant.KeyCount;
                    nextPart += variant.PartCount;
                }
            }

            if (nextArgument != MessageArguments.Length ||
                nextPart != MessageParts.Length ||
                nextSelector != Selectors.Length ||
                nextVariant != Variants.Length ||
                nextKey != VariantKeys.Length)
            {
                throw Invalid("message layout");
            }
        }

        private void ValidateAsset(I18nCompiledAssetRecord asset)
        {
            if (!asset.HasValue)
            {
                if (asset.LocalFileId != I18nCompiledCatalogFormat.MissingIndex)
                {
                    throw Invalid("asset reference");
                }

                return;
            }

            ValidateString(asset.AssetGuid);
            ValidateOptionalIndex(asset.LocalFileId, Strings.Length, "asset local file ID");
        }

        private void ValidateString(int index)
        {
            ValidateIndex(index, Strings.Length, "string index");
        }

        private static void ValidateRange(int start, int count, int length, string name)
        {
            if (start < 0 || count < 0 || start > length - count)
            {
                throw Invalid(name);
            }
        }

        private static void ValidateOptionalIndex(int index, int length, string name)
        {
            if (index != I18nCompiledCatalogFormat.MissingIndex)
            {
                ValidateIndex(index, length, name);
            }
        }

        private static void ValidateIndex(int index, int length, string name)
        {
            if (index < 0 || index >= length)
            {
                throw Invalid(name);
            }
        }

        private static InvalidDataException Invalid(string name)
        {
            return new InvalidDataException($"Compiled localization data contains an invalid {name}.");
        }
    }

    internal readonly struct I18nCompiledAssetRecord
    {
        internal I18nCompiledAssetRecord(int assetGuid, int localFileId)
        {
            AssetGuid = assetGuid;
            LocalFileId = localFileId;
        }

        internal int AssetGuid { get; }
        internal int LocalFileId { get; }
        internal bool HasValue => AssetGuid != I18nCompiledCatalogFormat.MissingIndex;
        internal static I18nCompiledAssetRecord Empty =>
            new I18nCompiledAssetRecord(
                I18nCompiledCatalogFormat.MissingIndex,
                I18nCompiledCatalogFormat.MissingIndex);
    }

    internal readonly struct I18nCompiledLocaleRecord
    {
        internal I18nCompiledLocaleRecord(
            int id,
            int displayName,
            int cultureName,
            int fallbackLocale,
            int firstFallback,
            int fallbackCount,
            I18nCompiledAssetRecord icon)
        {
            Id = id;
            DisplayName = displayName;
            CultureName = cultureName;
            FallbackLocale = fallbackLocale;
            FirstFallback = firstFallback;
            FallbackCount = fallbackCount;
            Icon = icon;
        }

        internal int Id { get; }
        internal int DisplayName { get; }
        internal int CultureName { get; }
        internal int FallbackLocale { get; }
        internal int FirstFallback { get; }
        internal int FallbackCount { get; }
        internal I18nCompiledAssetRecord Icon { get; }
    }

    internal readonly struct I18nCompiledEntryRecord
    {
        internal I18nCompiledEntryRecord(long id, int path, int firstValue, int valueCount)
        {
            Id = id;
            Path = path;
            FirstValue = firstValue;
            ValueCount = valueCount;
        }

        internal long Id { get; }
        internal int Path { get; }
        internal int FirstValue { get; }
        internal int ValueCount { get; }
    }

    internal readonly struct I18nCompiledValueRecord
    {
        internal I18nCompiledValueRecord(
            int locale,
            int message,
            I18nCompiledAssetRecord asset)
        {
            Locale = locale;
            Message = message;
            Asset = asset;
        }

        internal int Locale { get; }
        internal int Message { get; }
        internal I18nCompiledAssetRecord Asset { get; }
    }

    internal readonly struct I18nCompiledMessageRecord
    {
        internal I18nCompiledMessageRecord(
            int firstArgument,
            int argumentCount,
            int firstPart,
            int partCount,
            int firstSelector,
            int selectorCount,
            int firstVariant,
            int variantCount)
        {
            FirstArgument = firstArgument;
            ArgumentCount = argumentCount;
            FirstPart = firstPart;
            PartCount = partCount;
            FirstSelector = firstSelector;
            SelectorCount = selectorCount;
            FirstVariant = firstVariant;
            VariantCount = variantCount;
        }

        internal int FirstArgument { get; }
        internal int ArgumentCount { get; }
        internal int FirstPart { get; }
        internal int PartCount { get; }
        internal int FirstSelector { get; }
        internal int SelectorCount { get; }
        internal int FirstVariant { get; }
        internal int VariantCount { get; }
    }

    internal readonly struct I18nCompiledMessagePartRecord
    {
        internal I18nCompiledMessagePartRecord(
            I18nCompiledMessage.MessagePartKind kind,
            int value,
            int sourcePosition,
            int numberOptions)
        {
            Kind = kind;
            Value = value;
            SourcePosition = sourcePosition;
            NumberOptions = numberOptions;
        }

        internal I18nCompiledMessage.MessagePartKind Kind { get; }
        internal int Value { get; }
        internal int SourcePosition { get; }
        internal int NumberOptions { get; }
    }

    internal readonly struct I18nCompiledSelectorRecord
    {
        internal I18nCompiledSelectorRecord(
            int name,
            I18nCompiledMessage.MessageSelectorKind kind,
            int numberOptions)
        {
            Name = name;
            Kind = kind;
            NumberOptions = numberOptions;
        }

        internal int Name { get; }
        internal I18nCompiledMessage.MessageSelectorKind Kind { get; }
        internal int NumberOptions { get; }
    }

    internal readonly struct I18nCompiledVariantRecord
    {
        internal I18nCompiledVariantRecord(int firstKey, int keyCount, int firstPart, int partCount)
        {
            FirstKey = firstKey;
            KeyCount = keyCount;
            FirstPart = firstPart;
            PartCount = partCount;
        }

        internal int FirstKey { get; }
        internal int KeyCount { get; }
        internal int FirstPart { get; }
        internal int PartCount { get; }
    }

    internal readonly struct I18nCompiledVariantKeyRecord
    {
        internal I18nCompiledVariantKeyRecord(
            I18nCompiledMessage.MessageVariantKeyKind kind,
            int value,
            decimal exactNumber)
        {
            Kind = kind;
            Value = value;
            ExactNumber = exactNumber;
        }

        internal I18nCompiledMessage.MessageVariantKeyKind Kind { get; }
        internal int Value { get; }
        internal decimal ExactNumber { get; }
    }
}
