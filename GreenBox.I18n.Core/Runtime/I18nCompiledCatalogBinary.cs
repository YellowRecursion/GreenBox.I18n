using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GreenBox.I18n
{
    /// <summary>Reads and writes the versioned runtime catalog format.</summary>
    public static class I18nCompiledCatalogBinary
    {
        private const uint Magic = 0x31494247; // GBI1
        private const int CurrentVersion = 1;
        private const int MaximumCollectionSize = 10_000_000;

        /// <summary>Serializes a prepared catalog into the versioned binary runtime format.</summary>
        public static byte[] Serialize(I18nCompiledCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, new UTF8Encoding(false), true))
            {
                writer.Write(Magic);
                writer.Write(CurrentVersion);
                writer.Write(catalog.DefaultLocaleId);
                writer.Write(catalog.Locales.Count);
                for (int index = 0; index < catalog.Locales.Count; index++)
                {
                    I18nCompiledCatalog.CompiledLocale locale = catalog.Locales[index];
                    writer.Write(locale.Id);
                    writer.Write(locale.DisplayName);
                    writer.Write(locale.CultureName);
                    WriteNullableString(writer, locale.FallbackId);
                    WriteAsset(writer, locale.Icon);
                }

                writer.Write(catalog.Entries.Count);
                for (int entryIndex = 0; entryIndex < catalog.Entries.Count; entryIndex++)
                {
                    I18nCompiledCatalog.CompiledEntry entry = catalog.Entries[entryIndex];
                    writer.Write(entry.Id);
                    writer.Write(entry.Path);
                    writer.Write(entry.Values.Count);
                    for (int valueIndex = 0; valueIndex < entry.Values.Count; valueIndex++)
                    {
                        I18nCompiledCatalog.CompiledValue value = entry.Values[valueIndex];
                        writer.Write(value.LocaleId);
                        writer.Write(value.Message != null);
                        value.Message?.WriteTo(writer);
                        WriteAsset(writer, value.Asset);
                    }
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
                if (reader.ReadUInt32() != Magic)
                {
                    throw new InvalidDataException("The compiled localization catalog header is invalid.");
                }

                int version = reader.ReadInt32();
                if (version != CurrentVersion)
                {
                    throw new InvalidDataException(
                        $"Compiled localization catalog version {version} is unsupported; expected {CurrentVersion}. Regenerate the Unity asset.");
                }

                string defaultLocaleId = reader.ReadString();
                int localeCount = ReadCount(reader);
                var locales = new List<I18nCompiledCatalog.CompiledLocale>(localeCount);
                for (int index = 0; index < localeCount; index++)
                {
                    locales.Add(new I18nCompiledCatalog.CompiledLocale(
                        reader.ReadString(),
                        reader.ReadString(),
                        reader.ReadString(),
                        ReadNullableString(reader),
                        ReadAsset(reader)));
                }

                int entryCount = ReadCount(reader);
                var entries = new List<I18nCompiledCatalog.CompiledEntry>(entryCount);
                for (int entryIndex = 0; entryIndex < entryCount; entryIndex++)
                {
                    long id = reader.ReadInt64();
                    string path = reader.ReadString();
                    int valueCount = ReadCount(reader);
                    var values = new List<I18nCompiledCatalog.CompiledValue>(valueCount);
                    for (int valueIndex = 0; valueIndex < valueCount; valueIndex++)
                    {
                        string localeId = reader.ReadString();
                        I18nCompiledMessage? message = reader.ReadBoolean()
                            ? I18nCompiledMessage.ReadFrom(reader)
                            : null;
                        values.Add(new I18nCompiledCatalog.CompiledValue(
                            localeId,
                            message,
                            ReadAsset(reader)));
                    }

                    entries.Add(new I18nCompiledCatalog.CompiledEntry(id, path, values));
                }

                if (stream.Position != stream.Length)
                {
                    throw new InvalidDataException("Compiled localization catalog contains trailing data.");
                }

                return new I18nCompiledCatalog(defaultLocaleId, locales, entries);
            }
            catch (EndOfStreamException exception)
            {
                throw new InvalidDataException("Compiled localization catalog is truncated.", exception);
            }
        }

        private static void WriteAsset(BinaryWriter writer, I18nAssetReference? asset)
        {
            writer.Write(asset != null);
            if (asset == null)
            {
                return;
            }

            writer.Write(asset.AssetGuid);
            WriteNullableString(writer, asset.LocalFileId);
        }

        private static I18nAssetReference? ReadAsset(BinaryReader reader)
        {
            return !reader.ReadBoolean()
                ? null
                : new I18nAssetReference
                {
                    AssetGuid = reader.ReadString(),
                    LocalFileId = ReadNullableString(reader),
                };
        }

        private static void WriteNullableString(BinaryWriter writer, string? value)
        {
            writer.Write(value != null);
            if (value != null)
            {
                writer.Write(value);
            }
        }

        private static string? ReadNullableString(BinaryReader reader)
        {
            return reader.ReadBoolean() ? reader.ReadString() : null;
        }

        private static int ReadCount(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > MaximumCollectionSize)
            {
                throw new InvalidDataException("Compiled localization data contains an invalid collection size.");
            }

            return count;
        }
    }
}
