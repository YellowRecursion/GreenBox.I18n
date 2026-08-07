#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace GreenBox.I18n.Unity.Editor.Bridge
{
    /// <summary>
    /// Handles validated, project-scoped commands published by the GreenBox I18n Editor Host.
    /// </summary>
    [InitializeOnLoad]
    internal static class I18nEditorCommandListener
    {
        private const string CommandFormat = "greenbox.i18n.editor-command";
        private const int CommandFormatVersion = 1;
        private const int MaximumCommandsPerUpdate = 16;
        private static readonly TimeSpan MaximumCommandAge = TimeSpan.FromSeconds(15);
        private static readonly ConcurrentQueue<string> PendingRequests = new ConcurrentQueue<string>();
        private static readonly string RequestsPath;
        private static readonly string ResponsesPath;
        private static FileSystemWatcher? _watcher;

        static I18nEditorCommandListener()
        {
            string projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string bridgePath = Path.Combine(projectPath, "Library", "GreenBox.I18n", "Bridge");
            RequestsPath = Path.Combine(bridgePath, "requests");
            ResponsesPath = Path.Combine(bridgePath, "responses");

            Start();
            EditorApplication.update += ProcessPendingRequests;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
        }

        private static void Start()
        {
            try
            {
                Directory.CreateDirectory(RequestsPath);
                Directory.CreateDirectory(ResponsesPath);
                foreach (string requestPath in Directory.EnumerateFiles(RequestsPath, "*.json"))
                {
                    PendingRequests.Enqueue(requestPath);
                }

                _watcher = new FileSystemWatcher(RequestsPath, "*.json")
                {
                    IncludeSubdirectories = false,
                    NotifyFilter = NotifyFilters.FileName,
                    EnableRaisingEvents = true,
                };
                _watcher.Created += OnRequestPublished;
                _watcher.Renamed += OnRequestPublished;
            }
            catch (IOException)
            {
                StopWatcher();
            }
            catch (UnauthorizedAccessException)
            {
                StopWatcher();
            }
        }

        private static void OnRequestPublished(object sender, FileSystemEventArgs arguments)
        {
            PendingRequests.Enqueue(arguments.FullPath);
        }

        private static void ProcessPendingRequests()
        {
            for (int index = 0; index < MaximumCommandsPerUpdate; index++)
            {
                if (!PendingRequests.TryDequeue(out string requestPath))
                {
                    return;
                }

                ProcessRequest(requestPath);
            }
        }

        private static void ProcessRequest(string requestPath)
        {
            if (!File.Exists(requestPath))
            {
                return;
            }

            EditorCommand? command = null;
            EditorCommandResult result;
            try
            {
                command = JsonUtility.FromJson<EditorCommand>(File.ReadAllText(requestPath));
                result = Validate(command) ?? Execute(command);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                result = EditorCommandResult.Failed(exception.Message);
            }

            if (command != null && !string.IsNullOrWhiteSpace(command.commandId))
            {
                WriteResponse(command.commandId, result);
            }

            TryDelete(requestPath);
        }

        private static EditorCommandResult? Validate(EditorCommand? command)
        {
            if (command == null ||
                command.format != CommandFormat ||
                command.version != CommandFormatVersion ||
                string.IsNullOrWhiteSpace(command.commandId))
            {
                return EditorCommandResult.Failed("Unsupported editor command.");
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (command.createdAtUnixMilliseconds <= 0 ||
                Math.Abs(now - command.createdAtUnixMilliseconds) > MaximumCommandAge.TotalMilliseconds)
            {
                return EditorCommandResult.Failed("The editor command has expired.");
            }

            return null;
        }

        private static EditorCommandResult Execute(EditorCommand command)
        {
            switch (command.type)
            {
                case "open-code":
                    return OpenCode(command);
                case "open-asset":
                    return OpenAsset(command);
                default:
                    return EditorCommandResult.Failed("Unknown editor command type.");
            }
        }

        private static EditorCommandResult OpenCode(EditorCommand command)
        {
            string assetPath = NormalizeAssetPath(command.filePath);
            if (!IsAssetsPath(assetPath) ||
                !assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                return EditorCommandResult.Failed("The indexed C# file path is invalid.");
            }

            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
            if (script == null)
            {
                return EditorCommandResult.NotFound($"C# file '{assetPath}' was not found.");
            }

            int line = Math.Max(command.line, 1);
            return AssetDatabase.OpenAsset(script, line)
                ? EditorCommandResult.Opened()
                : EditorCommandResult.Failed($"Unity could not open '{assetPath}:{line.ToString(CultureInfo.InvariantCulture)}'.");
        }

        private static EditorCommandResult OpenAsset(EditorCommand command)
        {
            string assetPath = ResolveAssetPath(command);
            if (!IsAssetsPath(assetPath))
            {
                return EditorCommandResult.NotFound("The indexed Unity asset was not found.");
            }

            if (assetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                return OpenSceneUsage(assetPath, command);
            }

            if (assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                return OpenPrefabUsage(assetPath, command);
            }

            return OpenImportedAssetUsage(assetPath, command);
        }

        private static EditorCommandResult OpenSceneUsage(string assetPath, EditorCommand command)
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (!string.Equals(NormalizeAssetPath(scene.path), assetPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Object? target = FindTarget(scene.GetRootGameObjects(), command);
                return target != null
                    ? Select(target)
                    : EditorCommandResult.NotFound("The indexed object is no longer present in the open scene.");
            }

            return FocusAsset(
                assetPath,
                $"Scene '{assetPath}' was selected in Unity. Open it and try again.");
        }

        private static EditorCommandResult OpenPrefabUsage(string assetPath, EditorCommand command)
        {
            PrefabStage? stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null ||
                !string.Equals(NormalizeAssetPath(stage.assetPath), assetPath, StringComparison.OrdinalIgnoreCase))
            {
                return FocusAsset(
                    assetPath,
                    $"Prefab '{assetPath}' was selected in Unity. Open it and try again.");
            }

            Object? target = FindTarget(new[] { stage.prefabContentsRoot }, command);
            return target != null
                ? Select(target)
                : EditorCommandResult.NotFound("The indexed object is no longer present in the open prefab.");
        }

        private static EditorCommandResult OpenImportedAssetUsage(string assetPath, EditorCommand command)
        {
            if (!TryParseLocalId(command.assetLocalId, out long assetLocalId))
            {
                return EditorCommandResult.Failed("The indexed Unity object ID is invalid.");
            }

            foreach (Object candidate in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (MatchesLocalId(candidate, assetLocalId))
                {
                    return Select(candidate);
                }
            }

            return EditorCommandResult.NotFound("The indexed object is no longer present in the asset.");
        }

        private static Object? FindTarget(IEnumerable<GameObject> roots, EditorCommand command)
        {
            TryParseLocalId(command.assetLocalId, out long assetLocalId);
            TryParseLocalId(command.gameObjectLocalId, out long gameObjectLocalId);
            TryParseLocalId(command.targetLocalId, out long targetLocalId);
            GameObject? gameObjectFallback = null;

            foreach (GameObject root in roots)
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    GameObject gameObject = transform.gameObject;
                    if (command.isPrefabOverride &&
                        targetLocalId != 0 &&
                        MatchesPrefabSource(
                            gameObject,
                            command.targetAssetGuid,
                            targetLocalId,
                            assetLocalId))
                    {
                        return gameObject;
                    }

                    if (gameObjectLocalId != 0 && MatchesLocalId(gameObject, gameObjectLocalId))
                    {
                        gameObjectFallback = gameObject;
                    }

                    foreach (Component component in gameObject.GetComponents<Component>())
                    {
                        if (component != null &&
                            command.isPrefabOverride &&
                            targetLocalId != 0 &&
                            MatchesPrefabSource(
                                component,
                                command.targetAssetGuid,
                                targetLocalId,
                                assetLocalId))
                        {
                            return component;
                        }

                        if (component != null && assetLocalId != 0 && MatchesLocalId(component, assetLocalId))
                        {
                            return component;
                        }
                    }

                    if (assetLocalId != 0 && MatchesLocalId(gameObject, assetLocalId))
                    {
                        return gameObject;
                    }
                }
            }

            return gameObjectFallback ?? FindTargetByPath(roots, command.objectPath, command.componentType);
        }

        private static Object? FindTargetByPath(
            IEnumerable<GameObject> roots,
            string? objectPath,
            string? componentType)
        {
            if (string.IsNullOrWhiteSpace(objectPath))
            {
                return null;
            }

            string[] segments = objectPath!.Split(new[] { " / " }, StringSplitOptions.None);
            foreach (GameObject root in roots)
            {
                if (!string.Equals(root.name, segments[0], StringComparison.Ordinal))
                {
                    continue;
                }

                Transform current = root.transform;
                bool found = true;
                for (int index = 1; index < segments.Length; index++)
                {
                    Transform? child = null;
                    for (int childIndex = 0; childIndex < current.childCount; childIndex++)
                    {
                        Transform candidate = current.GetChild(childIndex);
                        if (string.Equals(candidate.name, segments[index], StringComparison.Ordinal))
                        {
                            child = candidate;
                            break;
                        }
                    }

                    if (child == null)
                    {
                        found = false;
                        break;
                    }

                    current = child;
                }

                if (!found)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(componentType))
                {
                    foreach (Component component in current.GetComponents<Component>())
                    {
                        if (component != null &&
                            string.Equals(component.GetType().Name, componentType, StringComparison.Ordinal))
                        {
                            return component;
                        }
                    }
                }

                return current.gameObject;
            }

            return null;
        }

        private static bool MatchesLocalId(Object target, long expectedLocalId)
        {
            GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(target);
            if (unchecked((long)globalId.targetObjectId) == expectedLocalId)
            {
                return true;
            }

            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out _, out long assetLocalId) &&
                   assetLocalId == expectedLocalId;
        }

        private static bool MatchesPrefabSource(
            Object instanceObject,
            string? expectedGuid,
            long expectedLocalId,
            long expectedPrefabInstanceId)
        {
            Object sourceObject = PrefabUtility.GetCorrespondingObjectFromOriginalSource(instanceObject);
            if (sourceObject == null ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    sourceObject,
                    out string sourceGuid,
                    out long sourceLocalId) ||
                sourceLocalId != expectedLocalId ||
                !string.Equals(sourceGuid, expectedGuid, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Object prefabInstance = PrefabUtility.GetPrefabInstanceHandle(instanceObject);
            return expectedPrefabInstanceId == 0 ||
                   (prefabInstance != null && MatchesLocalId(prefabInstance, expectedPrefabInstanceId));
        }

        private static EditorCommandResult Select(Object target)
        {
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
            SceneView.lastActiveSceneView?.FrameSelected();
            return EditorCommandResult.Opened();
        }

        private static EditorCommandResult FocusAsset(string assetPath, string message)
        {
            Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null)
            {
                return EditorCommandResult.NotFound($"Unity asset '{assetPath}' was not found.");
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
            return EditorCommandResult.RequiresUserAction(message);
        }

        private static string ResolveAssetPath(EditorCommand command)
        {
            if (!string.IsNullOrWhiteSpace(command.assetGuid))
            {
                string currentPath = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(command.assetGuid));
                if (!string.IsNullOrWhiteSpace(currentPath))
                {
                    return currentPath;
                }
            }

            return NormalizeAssetPath(command.assetPath);
        }

        private static string NormalizeAssetPath(string? path) =>
            (path ?? string.Empty).Replace('\\', '/').Trim();

        private static bool IsAssetsPath(string path) =>
            path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
            !path.Contains("../", StringComparison.Ordinal);

        private static bool TryParseLocalId(string? value, out long localId) =>
            long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out localId);

        private static void WriteResponse(string commandId, EditorCommandResult result)
        {
            string responsePath = Path.Combine(ResponsesPath, commandId + ".json");
            string temporaryPath = responsePath + ".tmp";
            try
            {
                var response = new EditorCommandResponse
                {
                    commandId = commandId,
                    status = result.Status,
                    message = result.Message,
                };
                File.WriteAllText(temporaryPath, JsonUtility.ToJson(response, true));
                if (File.Exists(responsePath))
                {
                    File.Delete(responsePath);
                }

                File.Move(temporaryPath, responsePath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            finally
            {
                TryDelete(temporaryPath);
            }
        }

        private static void Stop()
        {
            EditorApplication.update -= ProcessPendingRequests;
            StopWatcher();
        }

        private static void StopWatcher()
        {
            if (_watcher == null)
            {
                return;
            }

            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnRequestPublished;
            _watcher.Renamed -= OnRequestPublished;
            _watcher.Dispose();
            _watcher = null;
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        [Serializable]
        private sealed class EditorCommand
        {
            public string format = string.Empty;
            public int version;
            public string commandId = string.Empty;
            public long createdAtUnixMilliseconds;
            public string type = string.Empty;
            public string? filePath;
            public int line;
            public string? assetPath;
            public string? assetGuid;
            public string? assetLocalId;
            public string? gameObjectLocalId;
            public bool isPrefabOverride;
            public string? targetAssetGuid;
            public string? targetLocalId;
            public string? objectPath;
            public string? componentType;
        }

        [Serializable]
        private sealed class EditorCommandResponse
        {
            public string commandId = string.Empty;
            public string status = string.Empty;
            public string? message;
        }

        private sealed class EditorCommandResult
        {
            private EditorCommandResult(string status, string? message)
            {
                Status = status;
                Message = message;
            }

            internal string Status { get; }
            internal string? Message { get; }

            internal static EditorCommandResult Opened() => new EditorCommandResult("opened", null);

            internal static EditorCommandResult RequiresUserAction(string message) =>
                new EditorCommandResult("requiresUserAction", message);

            internal static EditorCommandResult NotFound(string message) =>
                new EditorCommandResult("notFound", message);

            internal static EditorCommandResult Failed(string message) =>
                new EditorCommandResult("failed", message);
        }
    }
}
