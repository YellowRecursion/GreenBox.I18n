#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace GreenBox.I18n.Unity.Editor.Usage
{
    /// <summary>
    /// Rejects results produced for an older Unity change notification.
    /// File stamps independently cover external edits that Unity has not imported yet.
    /// </summary>
    internal static class I18nUsageSourceRevisionTracker
    {
        private static readonly object Gate = new();
        private static readonly Dictionary<string, long> Revisions = new(
            StringComparer.OrdinalIgnoreCase);

        private static long _revision;

        internal static long CurrentRevision
        {
            get
            {
                lock (Gate)
                {
                    return _revision;
                }
            }
        }

        internal static void NotifyChanged(string sourceKey)
        {
            if (string.IsNullOrWhiteSpace(sourceKey))
            {
                return;
            }

            lock (Gate)
            {
                long revision = ++_revision;
                Revisions[sourceKey] = revision;
            }
        }

        internal static I18nUsageRevisionBatch Capture(IEnumerable<string> sourceKeys)
        {
            if (sourceKeys == null)
            {
                throw new ArgumentNullException(nameof(sourceKeys));
            }

            lock (Gate)
            {
                var revisions = sourceKeys
                    .Where(key => !string.IsNullOrWhiteSpace(key))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        key => key,
                        key => Revisions.TryGetValue(key, out long revision) ? revision : 0,
                        StringComparer.OrdinalIgnoreCase);
                return new I18nUsageRevisionBatch(revisions);
            }
        }

        internal static IReadOnlyList<string> FindChanged(I18nUsageRevisionBatch batch)
        {
            lock (Gate)
            {
                return batch.Revisions
                    .Where(pair =>
                        (Revisions.TryGetValue(pair.Key, out long revision) ? revision : 0) != pair.Value)
                    .Select(pair => pair.Key)
                    .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
        }
    }

    internal sealed class I18nUsageRevisionBatch
    {
        public I18nUsageRevisionBatch(IReadOnlyDictionary<string, long> revisions)
        {
            Revisions = revisions;
        }

        public IReadOnlyDictionary<string, long> Revisions { get; }
    }
}
