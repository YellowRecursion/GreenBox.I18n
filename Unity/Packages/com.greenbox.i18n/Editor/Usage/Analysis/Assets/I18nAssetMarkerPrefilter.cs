#nullable enable

using System;
using System.IO;
using System.Text;

namespace GreenBox.I18n.Usage.Analysis
{
    /// <summary>
    /// Rejects serialized assets that cannot contain an I18nKey before YAML parsing.
    /// </summary>
    internal static class I18nAssetMarkerPrefilter
    {
        private static readonly byte[] Marker = Encoding.UTF8.GetBytes("_greenBoxI18nEntryId");

        internal static bool Contains(string path, byte[] buffer)
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                1,
                FileOptions.SequentialScan);

            int matchedByteCount = 0;
            int readByteCount;
            while ((readByteCount = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int index = 0; index < readByteCount; index++)
                {
                    byte value = buffer[index];
                    if (value == Marker[matchedByteCount])
                    {
                        matchedByteCount++;
                        if (matchedByteCount == Marker.Length)
                        {
                            return true;
                        }

                        continue;
                    }

                    matchedByteCount = value == Marker[0] ? 1 : 0;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Reads the small subset of Unity meta data required by asset analysis.
    /// </summary>
    internal static class I18nAssetMetadataReader
    {
        internal static string ReadGuid(string metaPath)
        {
            if (!File.Exists(metaPath))
            {
                return string.Empty;
            }

            using var reader = new StreamReader(metaPath, Encoding.UTF8, true, 1024);
            while (reader.ReadLine() is { } line)
            {
                if (line.StartsWith("guid: ", StringComparison.Ordinal))
                {
                    return line.Substring("guid: ".Length).Trim();
                }
            }

            return string.Empty;
        }
    }
}
