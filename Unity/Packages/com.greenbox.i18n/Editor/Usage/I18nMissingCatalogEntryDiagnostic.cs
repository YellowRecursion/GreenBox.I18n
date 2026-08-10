#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GreenBox.I18n.Usage.Index;
using GreenBox.I18n.Unity.Editor.Catalogs;
using GreenBox.I18n.Unity.Editor.Compilation;
using GreenBox.I18n.Unity.Editor.Diagnostics;
using GreenBox.I18n.Unity.Editor.Settings;
using UnityEditor;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Usage
{
    /// <summary>
    /// Reports current project usages whose entry IDs are absent from the project catalog.
    /// </summary>
    [InitializeOnLoad]
    internal static class I18nMissingCatalogEntryDiagnostic
    {
        private const int MaximumReportedEntryCount = 10;
        private const int MaximumReportedLocationsPerEntry = 3;

        static I18nMissingCatalogEntryDiagnostic()
        {
            I18nUsageIndexController.IndexChanged += Evaluate;
            I18nCatalogCompilationEvents.CompilationFinished += HandleCatalogCompilationFinished;
            I18nProjectSettings.SourceCatalogChanged += Evaluate;
        }

        private static void HandleCatalogCompilationFinished(
            I18nCatalogAsset catalogAsset,
            I18nCatalogCompilationResult result)
        {
            if (result.IsSuccess && catalogAsset == I18nProjectSettings.instance.ProjectCatalog)
            {
                Evaluate();
            }
        }

        private static void Evaluate()
        {
            try
            {
                I18nUsageIndexSnapshot snapshot = I18nUsageIndexController.ReadCurrentUsages();
                if (!snapshot.IsReady)
                {
                    return;
                }

                I18nCatalog? catalog = I18nEditorCatalogProvider.GetCatalog(out _);
                if (catalog == null)
                {
                    return;
                }

                if (catalog.Entries == null)
                {
                    return;
                }

                var catalogEntryIds = new HashSet<long>();
                foreach (I18nEntry entry in catalog.Entries)
                {
                    if (entry != null && I18nEntryId.TryParse(entry.Id, out long entryId))
                    {
                        catalogEntryIds.Add(entryId);
                    }
                }

                IGrouping<long, I18nIndexedUsage>[] missingEntries = snapshot.Usages
                    .Where(usage => !catalogEntryIds.Contains(usage.EntryId))
                    .GroupBy(usage => usage.EntryId)
                    .OrderBy(group => group.Key)
                    .ToArray();
                if (missingEntries.Length == 0)
                {
                    return;
                }

                I18nLog.Warning(FormatWarning(missingEntries));
            }
            catch (Exception exception)
            {
                I18nLog.Error(
                    $"Failed to check usages against the project catalog: " +
                    $"{exception.GetType().Name}: {exception.Message}");
            }
        }

        private static string FormatWarning(
            IReadOnlyList<IGrouping<long, I18nIndexedUsage>> missingEntries)
        {
            var builder = new StringBuilder();
            builder.Append(missingEntries.Count)
                .Append(missingEntries.Count == 1 ? " entry ID is" : " entry IDs are")
                .Append(" used by the project but missing from the project catalog.");

            foreach (IGrouping<long, I18nIndexedUsage> entry in missingEntries
                         .Take(MaximumReportedEntryCount))
            {
                I18nIndexedUsage[] usages = entry.ToArray();
                builder.AppendLine()
                    .Append("  ")
                    .Append(entry.Key)
                    .Append(" - ")
                    .Append(usages.Length)
                    .Append(usages.Length == 1 ? " usage" : " usages");
                foreach (I18nIndexedUsage usage in usages.Take(MaximumReportedLocationsPerEntry))
                {
                    builder.AppendLine()
                        .Append("    ")
                        .Append(FormatLocation(usage));
                }

                if (usages.Length > MaximumReportedLocationsPerEntry)
                {
                    builder.AppendLine()
                        .Append("    ... and ")
                        .Append(usages.Length - MaximumReportedLocationsPerEntry)
                        .Append(" more");
                }
            }

            if (missingEntries.Count > MaximumReportedEntryCount)
            {
                builder.AppendLine()
                    .Append("  ... and ")
                    .Append(missingEntries.Count - MaximumReportedEntryCount)
                    .Append(" more missing IDs");
            }

            return builder.ToString();
        }

        private static string FormatLocation(I18nIndexedUsage usage)
        {
            string location = FormatFileLink(usage.FilePath, usage.Line);
            if (usage.Kind == I18nIndexedUsageKind.Code)
            {
                return location;
            }

            string context = string.Join(
                " / ",
                new[] { usage.ObjectPath, usage.ComponentType, usage.PropertyPath }
                    .Where(value => !string.IsNullOrEmpty(value))
                    .Select(EscapeRichText));
            return context.Length == 0 ? location : $"{location} - {context}";
        }

        private static string FormatFileLink(string filePath, int line)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return "Unknown location";
            }

            string targetPath = Path.IsPathRooted(filePath)
                ? filePath
                : Path.GetFullPath(Path.Combine(
                    Directory.GetParent(Application.dataPath)!.FullName,
                    filePath));
            string displayText = line > 0 ? $"{filePath}:{line}" : filePath;
            string lineAttribute = line > 0 ? $" line=\"{line}\"" : string.Empty;
            return $"<color=#40a0ff><a href=\"{EscapeRichText(targetPath)}\"{lineAttribute}>" +
                   $"{EscapeRichText(displayText)}</a></color>";
        }

        private static string EscapeRichText(string value)
        {
            return value
                .Replace("&", "&amp;")
                .Replace("\"", "&quot;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }
    }
}
