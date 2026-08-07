#nullable enable

using System;
using System.IO;

namespace GreenBox.I18n.Usage.Analysis
{
    /// <summary>
    /// Quickly detects assemblies that may contain self-identifying entry ID constants.
    /// </summary>
    internal static class I18nIlAssemblyPrefilter
    {
        private const byte LoadInt64Opcode = 0x21;
        private const int Int64ByteCount = sizeof(long);
        private const int PatternByteCount = 1 + Int64ByteCount;
        private const int ReadBufferSize = 64 * 1024;

        internal static bool MayContainEntryId(string assemblyPath)
        {
            byte[] buffer = new byte[ReadBufferSize + PatternByteCount - 1];
            using var stream = new FileStream(
                assemblyPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                1,
                FileOptions.SequentialScan);
            return MayContainEntryId(stream, buffer);
        }

        internal static bool MayContainEntryId(Stream stream, byte[] buffer)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (buffer.Length < PatternByteCount)
            {
                throw new ArgumentException(
                    $"The read buffer must contain at least {PatternByteCount} bytes.",
                    nameof(buffer));
            }

            int retainedByteCount = 0;
            int readByteCount;
            while ((readByteCount = stream.Read(
                       buffer,
                       retainedByteCount,
                       buffer.Length - retainedByteCount)) > 0)
            {
                int availableByteCount = retainedByteCount + readByteCount;
                int lastPatternStart = availableByteCount - PatternByteCount;
                for (int index = 0; index <= lastPatternStart; index++)
                {
                    if (buffer[index] != LoadInt64Opcode)
                    {
                        continue;
                    }

                    long value = ReadInt64LittleEndian(buffer, index + 1);
                    if (I18nEntryId.IsValid(value))
                    {
                        return true;
                    }
                }

                retainedByteCount = Math.Min(PatternByteCount - 1, availableByteCount);
                Buffer.BlockCopy(
                    buffer,
                    availableByteCount - retainedByteCount,
                    buffer,
                    0,
                    retainedByteCount);
            }

            return false;
        }

        private static long ReadInt64LittleEndian(byte[] bytes, int offset)
        {
            ulong value = bytes[offset]
                          | ((ulong)bytes[offset + 1] << 8)
                          | ((ulong)bytes[offset + 2] << 16)
                          | ((ulong)bytes[offset + 3] << 24)
                          | ((ulong)bytes[offset + 4] << 32)
                          | ((ulong)bytes[offset + 5] << 40)
                          | ((ulong)bytes[offset + 6] << 48)
                          | ((ulong)bytes[offset + 7] << 56);
            return unchecked((long)value);
        }
    }
}
