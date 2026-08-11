using System;
using System.IO;
using System.Text;

namespace GreenBox.I18n
{
    /// <summary>Reads and writes the versioned runtime catalog format.</summary>
    public static class I18nCompiledCatalogBinary
    {
        /// <summary>
        /// Gets a stable fingerprint that consumers must include in generated-catalog caches.
        /// </summary>
        public static string CompilerFingerprint =>
            I18nCompiledCatalogFormat.BinaryVersion + "." +
            I18nCompiledCatalogFormat.CompilerVersion;

        /// <summary>Serializes a prepared catalog into the versioned binary runtime format.</summary>
        public static byte[] Serialize(I18nCompiledCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            I18nCompiledCatalogStorage storage = catalog.Storage;
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, new UTF8Encoding(false), true))
            {
                writer.Write(I18nCompiledCatalogFormat.Magic);
                writer.Write(I18nCompiledCatalogFormat.BinaryVersion);
                writer.Write(I18nCompiledCatalogFormat.CompilerVersion);
                writer.Write(storage.DefaultLocale);

                writer.Write(storage.Strings.Length);
                for (int index = 0; index < storage.Strings.Length; index++)
                {
                    writer.Write(storage.Strings[index]);
                }

                writer.Write(storage.Locales.Length);
                for (int index = 0; index < storage.Locales.Length; index++)
                {
                    I18nCompiledLocaleRecord locale = storage.Locales[index];
                    writer.Write(locale.Id);
                    writer.Write(locale.DisplayName);
                    writer.Write(locale.CultureName);
                    writer.Write(locale.FallbackLocale);
                    writer.Write(locale.FirstFallback);
                    writer.Write(locale.FallbackCount);
                    WriteAsset(writer, locale.Icon);
                }

                WriteIntegers(writer, storage.FallbackLocales);

                writer.Write(storage.Entries.Length);
                for (int index = 0; index < storage.Entries.Length; index++)
                {
                    I18nCompiledEntryRecord entry = storage.Entries[index];
                    writer.Write(entry.Id);
                    writer.Write(entry.Path);
                    writer.Write(entry.FirstValue);
                    writer.Write(entry.ValueCount);
                }

                writer.Write(storage.Values.Length);
                for (int index = 0; index < storage.Values.Length; index++)
                {
                    I18nCompiledValueRecord value = storage.Values[index];
                    writer.Write(value.Locale);
                    writer.Write(value.Message);
                    WriteAsset(writer, value.Asset);
                }

                writer.Write(storage.Messages.Length);
                for (int index = 0; index < storage.Messages.Length; index++)
                {
                    I18nCompiledMessageRecord message = storage.Messages[index];
                    writer.Write(message.FirstArgument);
                    writer.Write(message.ArgumentCount);
                    writer.Write(message.FirstPart);
                    writer.Write(message.PartCount);
                    writer.Write(message.FirstSelector);
                    writer.Write(message.SelectorCount);
                    writer.Write(message.FirstVariant);
                    writer.Write(message.VariantCount);
                }

                WriteIntegers(writer, storage.MessageArguments);

                writer.Write(storage.NumberOptions.Length);
                for (int index = 0; index < storage.NumberOptions.Length; index++)
                {
                    I18nCompiledMessage.WriteNumberOptions(writer, storage.NumberOptions[index]);
                }

                writer.Write(storage.MessageParts.Length);
                for (int index = 0; index < storage.MessageParts.Length; index++)
                {
                    I18nCompiledMessagePartRecord part = storage.MessageParts[index];
                    writer.Write((byte)part.Kind);
                    writer.Write(part.Value);
                    writer.Write(part.SourcePosition);
                    writer.Write(part.NumberOptions);
                }

                writer.Write(storage.Selectors.Length);
                for (int index = 0; index < storage.Selectors.Length; index++)
                {
                    I18nCompiledSelectorRecord selector = storage.Selectors[index];
                    writer.Write(selector.Name);
                    writer.Write((byte)selector.Kind);
                    writer.Write(selector.NumberOptions);
                }

                writer.Write(storage.Variants.Length);
                for (int index = 0; index < storage.Variants.Length; index++)
                {
                    I18nCompiledVariantRecord variant = storage.Variants[index];
                    writer.Write(variant.FirstKey);
                    writer.Write(variant.KeyCount);
                    writer.Write(variant.FirstPart);
                    writer.Write(variant.PartCount);
                }

                writer.Write(storage.VariantKeys.Length);
                for (int index = 0; index < storage.VariantKeys.Length; index++)
                {
                    I18nCompiledVariantKeyRecord key = storage.VariantKeys[index];
                    writer.Write((byte)key.Kind);
                    writer.Write(key.Value);
                    I18nCompiledMessage.WriteDecimal(writer, key.ExactNumber);
                }
            }

            return stream.ToArray();
        }

        /// <summary>Deserializes a catalog produced by <see cref="Serialize"/>.</summary>
        public static I18nCompiledCatalog Deserialize(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            try
            {
                using var stream = new MemoryStream(data, false);
                using var reader = new BinaryReader(stream, Encoding.UTF8, false);
                if (reader.ReadUInt32() != I18nCompiledCatalogFormat.Magic)
                {
                    throw new InvalidDataException("The compiled localization catalog header is invalid.");
                }

                int binaryVersion = reader.ReadInt32();
                if (binaryVersion != I18nCompiledCatalogFormat.BinaryVersion)
                {
                    throw new InvalidDataException(
                        $"Compiled localization catalog version {binaryVersion} is unsupported; " +
                        $"expected {I18nCompiledCatalogFormat.BinaryVersion}. Regenerate the Unity asset.");
                }

                int compilerVersion = reader.ReadInt32();
                if (compilerVersion != I18nCompiledCatalogFormat.CompilerVersion)
                {
                    throw new InvalidDataException(
                        $"Compiled localization catalog compiler version {compilerVersion} is unsupported; " +
                        $"expected {I18nCompiledCatalogFormat.CompilerVersion}. Regenerate the Unity asset.");
                }

                int defaultLocale = reader.ReadInt32();
                var strings = new string[ReadCount(reader)];
                for (int index = 0; index < strings.Length; index++)
                {
                    strings[index] = reader.ReadString();
                }

                var locales = new I18nCompiledLocaleRecord[ReadCount(reader)];
                for (int index = 0; index < locales.Length; index++)
                {
                    locales[index] = new I18nCompiledLocaleRecord(
                        reader.ReadInt32(),
                        reader.ReadInt32(),
                        reader.ReadInt32(),
                        reader.ReadInt32(),
                        reader.ReadInt32(),
                        reader.ReadInt32(),
                        ReadAsset(reader));
                }

                int[] fallbacks = ReadIntegers(reader);

                var entries = new I18nCompiledEntryRecord[ReadCount(reader)];
                for (int index = 0; index < entries.Length; index++)
                {
                    entries[index] = new I18nCompiledEntryRecord(
                        reader.ReadInt64(),
                        reader.ReadInt32(),
                        reader.ReadInt32(),
                        reader.ReadInt32());
                }

                var values = new I18nCompiledValueRecord[ReadCount(reader)];
                for (int index = 0; index < values.Length; index++)
                {
                    values[index] = new I18nCompiledValueRecord(
                        reader.ReadInt32(),
                        reader.ReadInt32(),
                        ReadAsset(reader));
                }

                var messages = new I18nCompiledMessageRecord[ReadCount(reader)];
                for (int index = 0; index < messages.Length; index++)
                {
                    messages[index] = new I18nCompiledMessageRecord(
                        reader.ReadInt32(),
                        reader.ReadInt32(),
                        reader.ReadInt32(),
                        reader.ReadInt32(),
                        reader.ReadInt32(),
                        reader.ReadInt32(),
                        reader.ReadInt32(),
                        reader.ReadInt32());
                }

                int[] messageArguments = ReadIntegers(reader);

                var numberOptions = new I18nCompiledMessage.NumberOptions[ReadCount(reader)];
                for (int index = 0; index < numberOptions.Length; index++)
                {
                    numberOptions[index] = I18nCompiledMessage.ReadNumberOptions(reader);
                }

                var messageParts = new I18nCompiledMessagePartRecord[ReadCount(reader)];
                for (int index = 0; index < messageParts.Length; index++)
                {
                    messageParts[index] = new I18nCompiledMessagePartRecord(
                        (I18nCompiledMessage.MessagePartKind)reader.ReadByte(),
                        reader.ReadInt32(),
                        reader.ReadInt32(),
                        reader.ReadInt32());
                }

                var selectors = new I18nCompiledSelectorRecord[ReadCount(reader)];
                for (int index = 0; index < selectors.Length; index++)
                {
                    selectors[index] = new I18nCompiledSelectorRecord(
                        reader.ReadInt32(),
                        (I18nCompiledMessage.MessageSelectorKind)reader.ReadByte(),
                        reader.ReadInt32());
                }

                var variants = new I18nCompiledVariantRecord[ReadCount(reader)];
                for (int index = 0; index < variants.Length; index++)
                {
                    variants[index] = new I18nCompiledVariantRecord(
                        reader.ReadInt32(),
                        reader.ReadInt32(),
                        reader.ReadInt32(),
                        reader.ReadInt32());
                }

                var variantKeys = new I18nCompiledVariantKeyRecord[ReadCount(reader)];
                for (int index = 0; index < variantKeys.Length; index++)
                {
                    variantKeys[index] = new I18nCompiledVariantKeyRecord(
                        (I18nCompiledMessage.MessageVariantKeyKind)reader.ReadByte(),
                        reader.ReadInt32(),
                        I18nCompiledMessage.ReadDecimal(reader));
                }

                if (stream.Position != stream.Length)
                {
                    throw new InvalidDataException("Compiled localization catalog contains trailing data.");
                }

                return new I18nCompiledCatalog(
                    new I18nCompiledCatalogStorage(
                        defaultLocale,
                        strings,
                        locales,
                        fallbacks,
                        entries,
                        values,
                        messages,
                        messageArguments,
                        numberOptions,
                        messageParts,
                        selectors,
                        variants,
                        variantKeys));
            }
            catch (EndOfStreamException exception)
            {
                throw new InvalidDataException("Compiled localization catalog is truncated.", exception);
            }
        }

        private static void WriteAsset(BinaryWriter writer, I18nCompiledAssetRecord asset)
        {
            writer.Write(asset.AssetGuid);
            writer.Write(asset.LocalFileId);
        }

        private static I18nCompiledAssetRecord ReadAsset(BinaryReader reader)
        {
            return new I18nCompiledAssetRecord(reader.ReadInt32(), reader.ReadInt32());
        }

        private static void WriteIntegers(BinaryWriter writer, int[] values)
        {
            writer.Write(values.Length);
            for (int index = 0; index < values.Length; index++)
            {
                writer.Write(values[index]);
            }
        }

        private static int[] ReadIntegers(BinaryReader reader)
        {
            var values = new int[ReadCount(reader)];
            for (int index = 0; index < values.Length; index++)
            {
                values[index] = reader.ReadInt32();
            }

            return values;
        }

        private static int ReadCount(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > I18nCompiledCatalogFormat.MaximumCollectionSize)
            {
                throw new InvalidDataException("Compiled localization data contains an invalid collection size.");
            }

            return count;
        }
    }
}
