using System;
using System.Globalization;
using System.Security.Cryptography;

namespace GreenBox.I18n
{
    /// <summary>
    /// Recognizes self-identifying GreenBox I18n entry IDs.
    /// </summary>
    public static class I18nEntryId
    {
        private const int Magic = 0x6B1;
        private const int CurrentVersion = 0;
        private const int VersionBitCount = 3;
        private const int PayloadBitCount = 40;
        private const int ChecksumBitCount = 8;
        private const ulong PayloadMask = (1UL << PayloadBitCount) - 1;

        /// <summary>
        /// Generates a cryptographically random entry ID using the current format.
        /// </summary>
        /// <returns>A new self-identifying entry ID.</returns>
        public static long Generate()
        {
            Span<byte> bytes = stackalloc byte[5];
            RandomNumberGenerator.Fill(bytes);

            ulong payload = 0;
            for (int byteIndex = 0; byteIndex < bytes.Length; byteIndex++)
            {
                payload = (payload << 8) | bytes[byteIndex];
            }

            return Create(payload);
        }

        /// <summary>
        /// Determines whether a value uses the current self-identifying entry ID format.
        /// </summary>
        /// <param name="value">The numeric value to inspect.</param>
        /// <returns><see langword="true"/> when the signature, version, and checksum are valid.</returns>
        public static bool IsValid(long value)
        {
            if (value <= 0)
            {
                return false;
            }

            ulong rawValue = (ulong)value;
            ulong body = rawValue >> ChecksumBitCount;
            int header = (int)(body >> PayloadBitCount);
            int magic = header >> VersionBitCount;
            int version = header & ((1 << VersionBitCount) - 1);
            byte checksum = (byte)(rawValue & byte.MaxValue);

            return magic == Magic &&
                   version == CurrentVersion &&
                   checksum == ComputeChecksum(body);
        }

        /// <summary>
        /// Parses a decimal entry ID and verifies its embedded signature and checksum.
        /// </summary>
        /// <param name="value">The decimal ID text.</param>
        /// <param name="id">Receives the parsed ID when valid.</param>
        /// <returns><see langword="true"/> when the text contains a valid entry ID.</returns>
        public static bool TryParse(string? value, out long id)
        {
            return long.TryParse(
                       value,
                       NumberStyles.None,
                       CultureInfo.InvariantCulture,
                       out id) &&
                   IsValid(id);
        }

        internal static long Create(ulong randomPayload)
        {
            if (randomPayload > PayloadMask)
            {
                throw new ArgumentOutOfRangeException(nameof(randomPayload));
            }

            int header = (Magic << VersionBitCount) | CurrentVersion;
            ulong body = ((ulong)header << PayloadBitCount) | randomPayload;
            return (long)((body << ChecksumBitCount) | ComputeChecksum(body));
        }

        private static byte ComputeChecksum(ulong body)
        {
            byte checksum = 0;
            for (int byteIndex = 6; byteIndex >= 0; byteIndex--)
            {
                checksum ^= (byte)(body >> (byteIndex * 8));
                for (int bitIndex = 0; bitIndex < 8; bitIndex++)
                {
                    checksum = (byte)((checksum & 0x80) != 0
                        ? (checksum << 1) ^ 0x07
                        : checksum << 1);
                }
            }

            return checksum;
        }
    }
}
