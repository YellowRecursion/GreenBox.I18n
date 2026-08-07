#nullable enable

using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Diagnostics
{
    /// <summary>
    /// Writes consistently formatted GreenBox I18n messages to the Unity Console.
    /// </summary>
    internal static class I18nLog
    {
        private const string Prefix = "[i18n]";

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

        private static string Format(string message)
        {
            return $"{Prefix} {message}";
        }
    }
}
