#nullable enable

using GreenBox.I18n.Unity.Editor.Diagnostics;
using UnityEditor;

namespace GreenBox.I18n.Unity.Editor.Setup
{
    /// <summary>
    /// Performs the one-time project catalog setup after the package is installed.
    /// </summary>
    [InitializeOnLoad]
    internal static class I18nProjectBootstrap
    {
        private static bool _isScheduled;

        static I18nProjectBootstrap()
        {
            ScheduleRepair();
            EditorApplication.projectChanged += ScheduleRepair;
        }

        private static void ScheduleRepair()
        {
            if (_isScheduled)
            {
                return;
            }

            _isScheduled = true;
            EditorApplication.delayCall += EnsureInitialized;
        }

        private static void EnsureInitialized()
        {
            _isScheduled = false;
            if (I18nProjectSetup.IsHealthy())
            {
                return;
            }

            if (!I18nProjectSetup.TryRepair(out string error))
            {
                I18nLog.Warning("Automatic project catalog setup failed. " + error);
            }
        }
    }
}
