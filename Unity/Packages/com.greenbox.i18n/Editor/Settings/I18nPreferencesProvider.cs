#nullable enable

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Settings
{
    /// <summary>
    /// Draws local GreenBox I18n preferences in the Unity Preferences window.
    /// </summary>
    internal static class I18nPreferencesProvider
    {
        [SettingsProvider]
        private static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Preferences/GreenBox/i18n", SettingsScope.User)
            {
                label = "i18n",
                guiHandler = DrawSettings,
                keywords = new HashSet<string>
                {
                    "GreenBox",
                    "I18n",
                    "Localization",
                    "Usage",
                    "Indexing",
                    "Diagnostics",
                    "Logging",
                },
            };
        }

        private static void DrawSettings(string searchContext)
        {
            I18nPreferences preferences = I18nPreferences.instance;

            EditorGUILayout.HelpBox(
                "These preferences are stored locally for the current Unity user and are not shared with the project.",
                MessageType.Info);
            EditorGUILayout.Space();

            I18nUsageIndexingMode usageIndexing = (I18nUsageIndexingMode)EditorGUILayout.EnumPopup(
                new GUIContent(
                    "Usage Indexing",
                    "Controls when GreenBox I18n scans code and serialized assets for entry references."),
                preferences.UsageIndexing);
            if (usageIndexing != preferences.UsageIndexing)
            {
                preferences.UsageIndexing = usageIndexing;
            }

            EditorGUILayout.HelpBox(GetUsageIndexingHelp(usageIndexing), MessageType.None);
            EditorGUILayout.Space();

            I18nDiagnosticLogging diagnosticLogging =
                (I18nDiagnosticLogging)EditorGUILayout.EnumPopup(
                    new GUIContent(
                        "Diagnostic Logging",
                        "Controls the amount of GreenBox I18n diagnostic information written to the Unity Console."),
                    preferences.DiagnosticLogging);
            if (diagnosticLogging != preferences.DiagnosticLogging)
            {
                preferences.DiagnosticLogging = diagnosticLogging;
            }

            EditorGUILayout.HelpBox(GetDiagnosticLoggingHelp(diagnosticLogging), MessageType.None);
        }

        private static string GetUsageIndexingHelp(I18nUsageIndexingMode mode)
        {
            return mode switch
            {
                I18nUsageIndexingMode.Automatic =>
                    "Keeps the usage index up to date after code and serialized asset changes. " +
                    "Initial indexing may scan the entire project.",
                I18nUsageIndexingMode.Manual =>
                    "Scans only when you run Tools > GreenBox I18n > Scan Entry Usage.",
                I18nUsageIndexingMode.Disabled =>
                    "Disables usage scanning and reference discovery.",
                _ => string.Empty,
            };
        }

        private static string GetDiagnosticLoggingHelp(I18nDiagnosticLogging level)
        {
            return level switch
            {
                I18nDiagnosticLogging.Normal =>
                    "Shows actionable warnings, errors, and concise results for manual operations.",
                I18nDiagnosticLogging.Performance =>
                    "Also shows elapsed time, managed memory, scan stages, and slow files.",
                I18nDiagnosticLogging.Verbose =>
                    "Also lists detected entry usage locations. Console output is capped to prevent flooding.",
                _ => string.Empty,
            };
        }
    }
}
