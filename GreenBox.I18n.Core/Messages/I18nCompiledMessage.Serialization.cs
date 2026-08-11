using System.IO;

namespace GreenBox.I18n
{
    public sealed partial class I18nCompiledMessage
    {
        internal static void WriteNumberOptions(BinaryWriter writer, NumberOptions options)
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

        internal static NumberOptions ReadNumberOptions(BinaryReader reader)
        {
            return new NumberOptions(
                reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(),
                reader.ReadInt32(), reader.ReadInt32(), (NumberGrouping)reader.ReadByte(),
                (NumberSignDisplay)reader.ReadByte(), (TrailingZeroDisplay)reader.ReadByte(),
                (NumberRoundingMode)reader.ReadByte(), (NumberRoundingPriority)reader.ReadByte(),
                (NumberStyle)reader.ReadByte(), ReadDecimal(reader));
        }

        internal static void WriteDecimal(BinaryWriter writer, decimal value)
        {
            int[] bits = decimal.GetBits(value);
            for (int index = 0; index < bits.Length; index++)
            {
                writer.Write(bits[index]);
            }
        }

        internal static decimal ReadDecimal(BinaryReader reader)
        {
            int low = reader.ReadInt32();
            int middle = reader.ReadInt32();
            int high = reader.ReadInt32();
            int flags = reader.ReadInt32();
            int scale = (flags >> 16) & 0xFF;
            const int allowedFlags = int.MinValue | 0x00FF0000;
            if ((flags & ~allowedFlags) != 0 || scale > 28)
            {
                throw new InvalidDataException("Compiled localization data contains an invalid decimal value.");
            }

            return new decimal(low, middle, high, (flags & int.MinValue) != 0, (byte)scale);
        }
    }
}
