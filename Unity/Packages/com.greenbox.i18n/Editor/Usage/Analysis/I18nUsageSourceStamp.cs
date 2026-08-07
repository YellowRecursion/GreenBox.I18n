#nullable enable

using System;
using System.IO;

namespace GreenBox.I18n.Usage.Analysis
{
    /// <summary>
    /// A cheap observation used to reject a result when its source changes during analysis.
    /// It is a consistency guard, not a content cache key.
    /// </summary>
    internal readonly struct I18nUsageSourceStamp : IEquatable<I18nUsageSourceStamp>
    {
        private I18nUsageSourceStamp(
            bool exists,
            long length,
            long lastWriteTimeUtcTicks,
            bool auxiliaryExists,
            long auxiliaryLength,
            long auxiliaryLastWriteTimeUtcTicks)
        {
            Exists = exists;
            Length = length;
            LastWriteTimeUtcTicks = lastWriteTimeUtcTicks;
            AuxiliaryExists = auxiliaryExists;
            AuxiliaryLength = auxiliaryLength;
            AuxiliaryLastWriteTimeUtcTicks = auxiliaryLastWriteTimeUtcTicks;
        }

        public bool Exists { get; }

        public long Length { get; }

        public long LastWriteTimeUtcTicks { get; }

        public bool AuxiliaryExists { get; }

        public long AuxiliaryLength { get; }

        public long AuxiliaryLastWriteTimeUtcTicks { get; }

        internal static I18nUsageSourceStamp Capture(
            string sourcePath,
            string? auxiliaryPath = null)
        {
            (bool exists, long length, long writeTicks) = CaptureFile(sourcePath);
            (bool auxiliaryExists, long auxiliaryLength, long auxiliaryWriteTicks) =
                string.IsNullOrEmpty(auxiliaryPath)
                    ? (false, 0L, 0L)
                    : CaptureFile(auxiliaryPath!);
            return new I18nUsageSourceStamp(
                exists,
                length,
                writeTicks,
                auxiliaryExists,
                auxiliaryLength,
                auxiliaryWriteTicks);
        }

        public bool Equals(I18nUsageSourceStamp other)
        {
            return Exists == other.Exists &&
                   Length == other.Length &&
                   LastWriteTimeUtcTicks == other.LastWriteTimeUtcTicks &&
                   AuxiliaryExists == other.AuxiliaryExists &&
                   AuxiliaryLength == other.AuxiliaryLength &&
                   AuxiliaryLastWriteTimeUtcTicks == other.AuxiliaryLastWriteTimeUtcTicks;
        }

        public override bool Equals(object? obj)
        {
            return obj is I18nUsageSourceStamp other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Exists.GetHashCode();
                hashCode = (hashCode * 397) ^ Length.GetHashCode();
                hashCode = (hashCode * 397) ^ LastWriteTimeUtcTicks.GetHashCode();
                hashCode = (hashCode * 397) ^ AuxiliaryExists.GetHashCode();
                hashCode = (hashCode * 397) ^ AuxiliaryLength.GetHashCode();
                hashCode = (hashCode * 397) ^ AuxiliaryLastWriteTimeUtcTicks.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(I18nUsageSourceStamp left, I18nUsageSourceStamp right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(I18nUsageSourceStamp left, I18nUsageSourceStamp right)
        {
            return !left.Equals(right);
        }

        private static (bool Exists, long Length, long LastWriteTimeUtcTicks) CaptureFile(string path)
        {
            var file = new FileInfo(path);
            file.Refresh();
            return file.Exists
                ? (true, file.Length, file.LastWriteTimeUtc.Ticks)
                : (false, 0L, 0L);
        }
    }
}
