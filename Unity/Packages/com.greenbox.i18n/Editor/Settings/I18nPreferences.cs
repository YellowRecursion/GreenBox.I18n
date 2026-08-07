#nullable enable

using System;
using UnityEditor;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Settings
{
    internal enum I18nUsageIndexingMode
    {
        Automatic,
        Manual,
        Disabled,
    }

    internal enum I18nDiagnosticLogging
    {
        Normal,
        Performance,
        Verbose,
    }

    /// <summary>
    /// Stores local GreenBox I18n preferences for the current Unity user.
    /// </summary>
    [FilePath("GreenBox/I18n.asset", FilePathAttribute.Location.PreferencesFolder)]
    internal sealed class I18nPreferences : ScriptableSingleton<I18nPreferences>
    {
        internal static event Action<I18nUsageIndexingMode>? UsageIndexingChanged;

        [SerializeField]
        private I18nUsageIndexingMode _usageIndexing = I18nUsageIndexingMode.Manual;

        [SerializeField]
        private I18nDiagnosticLogging _diagnosticLogging = I18nDiagnosticLogging.Normal;

        internal I18nUsageIndexingMode UsageIndexing
        {
            get => _usageIndexing;
            set
            {
                if (_usageIndexing == value)
                {
                    return;
                }

                _usageIndexing = value;
                Save(true);
                UsageIndexingChanged?.Invoke(value);
            }
        }

        internal I18nDiagnosticLogging DiagnosticLogging
        {
            get => _diagnosticLogging;
            set
            {
                if (_diagnosticLogging == value)
                {
                    return;
                }

                _diagnosticLogging = value;
                Save(true);
            }
        }
    }
}
