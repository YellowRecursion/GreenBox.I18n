#nullable enable

using System.Collections.Generic;
using GreenBox.I18n.Unity.Editor.VersionControl.Git;
using UnityEditor;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Settings
{
    /// <summary>
    /// Draws local GreenBox I18n preferences in the Unity Preferences window.
    /// </summary>
    internal static class I18nPreferencesProvider
    {
        private const string GitInstallCommand = "i18n git install";
        private static I18nGitIntegrationStatus? _gitStatus;
        private static string _gitSetupError = string.Empty;

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
                    "Git",
                    "Merge",
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
            DrawGitIntegration();
        }

        private static void DrawGitIntegration()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Git merge", EditorStyles.boldLabel);

            _gitStatus ??= I18nGitIntegration.GetStatus();
            I18nGitIntegrationStatus status = _gitStatus.Value;
            EditorGUILayout.HelpBox(
                status.Message,
                status.State == I18nGitIntegrationState.Configured
                    ? MessageType.Info
                    : MessageType.Warning);

            if (!string.IsNullOrEmpty(_gitSetupError))
            {
                EditorGUILayout.HelpBox(_gitSetupError, MessageType.Error);
            }

            if (status.State == I18nGitIntegrationState.DriverNotConfigured &&
                GUILayout.Button("Configure Git Integration"))
            {
                _gitSetupError = I18nGitIntegration.TryConfigure(out string error)
                    ? string.Empty
                    : error;
                _gitStatus = I18nGitIntegration.GetStatus();
            }

            if (status.State != I18nGitIntegrationState.Configured)
            {
                EditorGUILayout.LabelField(
                    "After installing GreenBox Desktop Tools, you can also run:");
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.SelectableLabel(
                        GitInstallCommand,
                        EditorStyles.textField,
                        GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    if (GUILayout.Button("Copy", GUILayout.Width(56f)))
                    {
                        EditorGUIUtility.systemCopyBuffer = GitInstallCommand;
                    }
                }
            }

            if (GUILayout.Button("Refresh Git Status"))
            {
                _gitStatus = I18nGitIntegration.GetStatus();
                _gitSetupError = string.Empty;
            }
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
