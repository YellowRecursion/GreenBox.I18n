#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Bridge
{
    /// <summary>
    /// Holds a project-scoped lease while this Unity Editor process is running.
    /// </summary>
    [InitializeOnLoad]
    internal static class I18nEditorPresenceLease
    {
        private const string PresenceFormat = "greenbox.i18n.editor-presence";
        private const int PresenceFormatVersion = 1;
        private static FileStream? _lease;

        static I18nEditorPresenceLease()
        {
            Acquire();
            AssemblyReloadEvents.beforeAssemblyReload += Release;
            EditorApplication.quitting += Release;
        }

        private static void Acquire()
        {
            if (_lease != null)
            {
                return;
            }

            try
            {
                string leasePath = GetLeasePath();
                Directory.CreateDirectory(Path.GetDirectoryName(leasePath)!);
                var lease = new FileStream(
                    leasePath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.Read);
                lease.SetLength(0);

                int processId;
                long startedAtUnixMilliseconds;
                using (Process process = Process.GetCurrentProcess())
                {
                    processId = process.Id;
                    startedAtUnixMilliseconds = new DateTimeOffset(process.StartTime.ToUniversalTime())
                        .ToUnixTimeMilliseconds();
                }

                var payload = new PresencePayload
                {
                    Format = PresenceFormat,
                    Version = PresenceFormatVersion,
                    ProcessId = processId,
                    StartedAtUnixMilliseconds = startedAtUnixMilliseconds,
                };
                using (var writer = new StreamWriter(lease, new UTF8Encoding(false), 1024, true))
                {
                    writer.Write(JsonUtility.ToJson(payload, true));
                    writer.Flush();
                }

                lease.Flush(true);
                _lease = lease;
            }
            catch (IOException)
            {
                // Another Unity Editor process may already own this project lease.
            }
            catch (UnauthorizedAccessException)
            {
                // Presence is optional and must not make Unity project startup noisy.
            }
        }

        private static void Release()
        {
            _lease?.Dispose();
            _lease = null;
        }

        private static string GetLeasePath()
        {
            string projectDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectDirectory, "Library", "GreenBox.I18n", "unity-editor.lock");
        }

        [Serializable]
        private sealed class PresencePayload
        {
            public string Format = string.Empty;
            public int Version;
            public int ProcessId;
            public long StartedAtUnixMilliseconds;
        }
    }
}
