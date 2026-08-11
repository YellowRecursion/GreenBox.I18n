#nullable enable

using System.Collections.Generic;
using GreenBox.I18n;
using UnityEngine;

internal static class I18nRuntimeDiagnosticReporter
{
    private static readonly HashSet<string> Reported = new HashSet<string>();

    internal static string Report(long entryId, string localeId, I18nMessageFormatResult result)
    {
        for (int index = 0; index < result.Diagnostics.Count; index++)
        {
            I18nMessageDiagnostic diagnostic = result.Diagnostics[index];
            string key = entryId + "\n" + localeId + "\n" +
                         diagnostic.Code + "\n" + diagnostic.ArgumentName;
            if (!Reported.Add(key))
            {
                continue;
            }

            Debug.LogError(
                $"[i18n] Failed to format entry {entryId} for locale '{localeId}': " +
                $"{diagnostic.Message} Returning '{result.Text}'.");
        }

        return result.Text;
    }

    internal static void Clear()
    {
        Reported.Clear();
    }
}
