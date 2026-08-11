using System;
using System.Collections.Generic;
using System.IO;

namespace GreenBox.I18n
{
    public sealed partial class I18nCompiledMessage
    {
        internal void WriteTo(BinaryWriter writer)
        {
            WriteStrings(writer, _argumentNames);
            WriteParts(writer, _parts);
            writer.Write(_matcher != null);
            if (_matcher == null)
            {
                return;
            }

            writer.Write(_matcher.Selectors.Length);
            for (int index = 0; index < _matcher.Selectors.Length; index++)
            {
                MessageSelector selector = _matcher.Selectors[index];
                writer.Write(selector.Name);
                writer.Write((byte)selector.Kind);
                WriteNumberOptions(writer, selector.NumberOptions);
            }

            writer.Write(_matcher.Variants.Length);
            for (int variantIndex = 0; variantIndex < _matcher.Variants.Length; variantIndex++)
            {
                MessageVariant variant = _matcher.Variants[variantIndex];
                writer.Write(variant.Keys.Length);
                for (int keyIndex = 0; keyIndex < variant.Keys.Length; keyIndex++)
                {
                    MessageVariantKey key = variant.Keys[keyIndex];
                    writer.Write((byte)key.Kind);
                    writer.Write(key.Value);
                    WriteDecimal(writer, key.ExactNumber);
                }

                WriteParts(writer, variant.Parts);
            }
        }

        internal static I18nCompiledMessage ReadFrom(BinaryReader reader)
        {
            List<string> arguments = ReadStrings(reader);
            MessagePart[] parts = ReadParts(reader);
            if (!reader.ReadBoolean())
            {
                return new I18nCompiledMessage(parts, null, arguments);
            }

            var selectors = new MessageSelector[ReadCount(reader)];
            for (int index = 0; index < selectors.Length; index++)
            {
                selectors[index] = new MessageSelector(
                    reader.ReadString(),
                    (MessageSelectorKind)reader.ReadByte(),
                    ReadNumberOptions(reader));
            }

            var variants = new MessageVariant[ReadCount(reader)];
            for (int variantIndex = 0; variantIndex < variants.Length; variantIndex++)
            {
                var keys = new MessageVariantKey[ReadCount(reader)];
                for (int keyIndex = 0; keyIndex < keys.Length; keyIndex++)
                {
                    keys[keyIndex] = new MessageVariantKey(
                        (MessageVariantKeyKind)reader.ReadByte(),
                        reader.ReadString(),
                        ReadDecimal(reader));
                }

                variants[variantIndex] = new MessageVariant(keys, ReadParts(reader));
            }

            return new I18nCompiledMessage(
                parts,
                new MessageMatcher(selectors, variants),
                arguments);
        }

        private static void WriteParts(BinaryWriter writer, MessagePart[] parts)
        {
            writer.Write(parts.Length);
            for (int index = 0; index < parts.Length; index++)
            {
                writer.Write((byte)parts[index].Kind);
                writer.Write(parts[index].Value);
                writer.Write(parts[index].SourcePosition);
                WriteNumberOptions(writer, parts[index].NumberOptions);
            }
        }

        private static MessagePart[] ReadParts(BinaryReader reader)
        {
            var parts = new MessagePart[ReadCount(reader)];
            for (int index = 0; index < parts.Length; index++)
            {
                parts[index] = new MessagePart(
                    (MessagePartKind)reader.ReadByte(),
                    reader.ReadString(),
                    reader.ReadInt32(),
                    ReadNumberOptions(reader));
            }

            return parts;
        }

        private static void WriteStrings(BinaryWriter writer, IReadOnlyList<string> values)
        {
            writer.Write(values.Count);
            for (int index = 0; index < values.Count; index++)
            {
                writer.Write(values[index]);
            }
        }

        private static List<string> ReadStrings(BinaryReader reader)
        {
            int count = ReadCount(reader);
            var values = new List<string>(count);
            for (int index = 0; index < count; index++)
            {
                values.Add(reader.ReadString());
            }

            return values;
        }

        private static void WriteNumberOptions(BinaryWriter writer, NumberOptions options)
        {
            writer.Write(options.MinimumFractionDigits);
            writer.Write(options.MaximumFractionDigits);
            writer.Write(options.MinimumSignificantDigits);
            writer.Write(options.MaximumSignificantDigits);
            writer.Write(options.MinimumIntegerDigits);
            writer.Write(options.RoundingIncrement);
            writer.Write((byte)options.Grouping);
            writer.Write((byte)options.SignDisplay);
            writer.Write((byte)options.TrailingZeroDisplay);
            writer.Write((byte)options.RoundingMode);
            writer.Write((byte)options.RoundingPriority);
            writer.Write((byte)options.Style);
            WriteDecimal(writer, options.Offset);
        }

        private static NumberOptions ReadNumberOptions(BinaryReader reader)
        {
            return new NumberOptions(
                reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(),
                reader.ReadInt32(), reader.ReadInt32(), (NumberGrouping)reader.ReadByte(),
                (NumberSignDisplay)reader.ReadByte(), (TrailingZeroDisplay)reader.ReadByte(),
                (NumberRoundingMode)reader.ReadByte(), (NumberRoundingPriority)reader.ReadByte(),
                (NumberStyle)reader.ReadByte(), ReadDecimal(reader));
        }

        private static void WriteDecimal(BinaryWriter writer, decimal value)
        {
            int[] bits = decimal.GetBits(value);
            for (int index = 0; index < bits.Length; index++)
            {
                writer.Write(bits[index]);
            }
        }

        private static decimal ReadDecimal(BinaryReader reader)
        {
            return new decimal(new[]
            {
                reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(),
            });
        }

        private static int ReadCount(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > 10_000_000)
            {
                throw new InvalidDataException("Compiled localization data contains an invalid collection size.");
            }

            return count;
        }
    }
}
