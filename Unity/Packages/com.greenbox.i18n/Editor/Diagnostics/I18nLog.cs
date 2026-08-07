#nullable enable

using GreenBox.I18n.Unity.Editor.Settings;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Diagnostics
{
    /// <summary>
    /// Writes consistently formatted GreenBox I18n messages to the Unity Console.
    /// </summary>
    internal static class I18nLog
    {
        private const string Prefix = "[i18n]";

        internal static bool IsPerformanceEnabled =>
            I18nPreferences.instance.DiagnosticLogging >= I18nDiagnosticLogging.Performance;

        internal static bool IsVerboseEnabled =>
            I18nPreferences.instance.DiagnosticLogging >= I18nDiagnosticLogging.Verbose;

        [HideInCallstack]
        internal static void Info(string message, Object? context = null)
        {
            Debug.Log(Format(message), context);
        }

        [HideInCallstack]
        internal static void Warning(string message, Object? context = null)
        {
            Debug.LogWarning(Format(message), context);
        }

        [HideInCallstack]
        internal static void Error(string message, Object? context = null)
        {
            Debug.LogError(Format(message), context);
        }

        [HideInCallstack]
        internal static void Performance(string message, Object? context = null)
        {
            if (IsPerformanceEnabled)
            {
                Debug.Log(Format(message), context);
            }
        }

        [HideInCallstack]
        internal static void Verbose(string message, Object? context = null)
        {
            if (IsVerboseEnabled)
            {
                Debug.Log(Format(message), context);
            }
        }

        private static string Format(string message)
        {
            return $"{Prefix} {message}";
        }
    }
}
