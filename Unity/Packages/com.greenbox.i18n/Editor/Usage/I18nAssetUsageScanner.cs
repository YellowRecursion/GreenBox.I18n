#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Usage
{
    /// <summary>
    /// Finds localization keys in Unity text-serialized scenes, prefabs, and assets.
    /// </summary>
    internal static class I18nAssetUsageScanner
    {
        private const string EntryIdPropertyName = "_greenBoxI18nEntryId";
        private const int ReadBufferSize = 64 * 1024;
        private const int MaximumReportedLocationCount = 100;
        private const int MaximumReportedLocationsPerEntry = 10;

        private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".asset",
            ".prefab",
            ".unity",
        };

        private static readonly byte[] EntryIdPropertyMarker = Encoding.UTF8.GetBytes(EntryIdPropertyName);

        internal static I18nAssetUsageScanResult Scan()
        {
            var profiler = new I18nUsageScanProfiler();
            var warnings = new List<string>();
            return Scan(
                EnumerateSerializedAssetPaths(Application.dataPath),
                profiler,
                warnings);
        }

        /// <summary>
        /// Scans only the specified Unity asset paths without producing console output.
        /// </summary>
        internal static I18nAssetUsageScanResult Scan(IReadOnlyList<string> assetPaths)
        {
            if (assetPaths == null)
            {
                throw new ArgumentNullException(nameof(assetPaths));
            }

            var profiler = new I18nUsageScanProfiler(false);
            var warnings = new List<string>();
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            return Scan(
                ResolveSerializedAssetPaths(assetPaths, projectRoot, warnings),
                profiler,
                warnings);
        }

        private static I18nAssetUsageScanResult Scan(
            IEnumerable<string> absolutePaths,
            I18nUsageScanProfiler profiler,
            List<string> warnings)
        {
            if (EditorSettings.serializationMode != SerializationMode.ForceText)
            {
                warnings.Add(
                    "Asset usage scanning requires Edit > Project Settings > Editor > " +
                    "Asset Serialization > Mode to be set to Force Text.");
                return new I18nAssetUsageScanResult(
                    Array.Empty<I18nAssetUsage>(),
                    warnings,
                    0,
                    0,
                    0,
                    profiler.Complete(),
                    false);
            }

            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            var models = new List<SerializedAssetModel>();
            var scriptTypeResolver = new ScriptTypeResolver();
            int scannedAssetCount = 0;
            long scannedByteCount = 0;
            byte[] readBuffer = new byte[ReadBufferSize];

            foreach (string absolutePath in EnumerateSerializedAssetPaths(Application.dataPath))
            {
                scannedAssetCount++;
                try
                {
                    scannedByteCount += new FileInfo(absolutePath).Length;
                    if (!ContainsEntryIdMarker(absolutePath, readBuffer))
                    {
                        continue;
                    }

                    string assetPath = ToAssetPath(absolutePath, projectRoot);
                    string assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                    SerializedAssetModel model = ParseAsset(
                        absolutePath,
                        assetPath,
                        assetGuid,
                        warnings);
                    profiler.Sample();
                    model.PrepareContexts(scriptTypeResolver);
                    profiler.Sample();
                    models.Add(model);
                }
                catch (Exception exception)
                {
                    warnings.Add(
                        $"Skipped '{ToAssetPath(absolutePath, projectRoot)}': " +
                        $"{exception.GetType().Name}: {exception.Message}");
                }
            }

            IReadOnlyDictionary<string, SerializedAssetModel> modelsByGuid = models
                .Where(model => model.AssetGuid.Length > 0)
                .GroupBy(model => model.AssetGuid, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            I18nAssetUsage[] usages = models
                .SelectMany(model => model.CreateUsages(modelsByGuid, scriptTypeResolver))
                .OrderBy(usage => usage.EntryId)
                .ThenBy(usage => usage.AssetPath, StringComparer.Ordinal)
                .ThenBy(usage => usage.Line)
                .ToArray();

            I18nUsageScanPerformance performance = profiler.Complete();
            return new I18nAssetUsageScanResult(
                usages,
                warnings,
                scannedAssetCount,
                models.Count,
                scannedByteCount,
                performance,
                true);
        }

        internal static string FormatReport(I18nAssetUsageScanResult result)
        {
            return FormatReport(result, MaximumReportedLocationCount, out _);
        }

        internal static string FormatReport(
            I18nAssetUsageScanResult result,
            int maximumReportedLocationCount,
            out int reportedLocationCount)
        {
            reportedLocationCount = 0;
            if (!result.IsForceText)
            {
                return "Asset usage scan was not run." + Environment.NewLine +
                       string.Join(Environment.NewLine, result.Warnings.Select(warning => $"  {warning}"));
            }

            var lines = new List<string>
            {
                $"Asset usage scan completed in {result.ElapsedMilliseconds} ms. " +
                $"Scanned {result.ScannedAssetCount} serialized asset file(s), " +
                $"{FormatByteCount(result.ScannedByteCount)} of source data; " +
                $"{result.MatchedAssetCount} contained I18nKey data; " +
                $"found {result.Usages.Count} usage(s) across " +
                $"{result.Usages.Select(usage => usage.EntryId).Distinct().Count()} ID(s).",
            };

            foreach (IGrouping<long, I18nAssetUsage> group in result.Usages
                         .GroupBy(usage => usage.EntryId)
                         .OrderBy(group => group.Key))
            {
                if (reportedLocationCount >= maximumReportedLocationCount)
                {
                    break;
                }

                int locationLimit = Math.Min(
                    MaximumReportedLocationsPerEntry,
                    maximumReportedLocationCount - reportedLocationCount);
                I18nAssetUsage[] reportedLocations = group.Take(locationLimit).ToArray();
                lines.Add($"Entry {group.Key}: {group.Count()} serialized usage(s)");
                lines.AddRange(reportedLocations.Select(FormatLocation));
                reportedLocationCount += reportedLocations.Length;
            }

            int omittedLocationCount = result.Usages.Count - reportedLocationCount;
            if (omittedLocationCount > 0)
            {
                lines.Add(
                    $"Asset output truncated: displayed {reportedLocationCount} of " +
                    $"{result.Usages.Count} locations; {omittedLocationCount} omitted.");
            }

            if (result.Warnings.Count > 0)
            {
                lines.Add("Warnings:");
                lines.AddRange(result.Warnings.Select(warning => $"  {warning}"));
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static IEnumerable<string> EnumerateSerializedAssetPaths(string assetsRoot)
        {
            return Directory
                .EnumerateFiles(assetsRoot, "*", SearchOption.AllDirectories)
                .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)));
        }

        private static IEnumerable<string> ResolveSerializedAssetPaths(
            IReadOnlyList<string> assetPaths,
            string projectRoot,
            ICollection<string> warnings)
        {
            var resolvedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string assetsRoot = Path.GetFullPath(Application.dataPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string assetsPrefix = assetsRoot + Path.DirectorySeparatorChar;

            foreach (string path in assetPaths)
            {
                if (string.IsNullOrWhiteSpace(path) ||
                    !SupportedExtensions.Contains(Path.GetExtension(path)))
                {
                    continue;
                }

                string absolutePath;
                try
                {
                    absolutePath = Path.GetFullPath(
                        Path.IsPathRooted(path)
                            ? path
                            : Path.Combine(projectRoot, path));
                }
                catch (Exception exception)
                {
                    warnings.Add($"Skipped invalid asset path '{path}': {exception.Message}");
                    continue;
                }

                if (!absolutePath.StartsWith(assetsPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add($"Skipped path outside Assets: '{path}'.");
                    continue;
                }

                if (!File.Exists(absolutePath))
                {
                    warnings.Add($"Skipped missing asset: '{ToAssetPath(absolutePath, projectRoot)}'.");
                    continue;
                }

                if (resolvedPaths.Add(absolutePath))
                {
                    yield return absolutePath;
                }
            }
        }

        private static bool ContainsEntryIdMarker(string path, byte[] buffer)
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                1,
                FileOptions.SequentialScan);

            int matchedByteCount = 0;
            int readByteCount;
            while ((readByteCount = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int index = 0; index < readByteCount; index++)
                {
                    byte value = buffer[index];
                    if (value == EntryIdPropertyMarker[matchedByteCount])
                    {
                        matchedByteCount++;
                        if (matchedByteCount == EntryIdPropertyMarker.Length)
                        {
                            return true;
                        }

                        continue;
                    }

                    matchedByteCount = value == EntryIdPropertyMarker[0] ? 1 : 0;
                }
            }

            return false;
        }

        private static SerializedAssetModel ParseAsset(
            string absolutePath,
            string assetPath,
            string assetGuid,
            ICollection<string> warnings)
        {
            var model = new SerializedAssetModel(assetPath, assetGuid);
            SerializedDocument? document = null;
            var propertyPath = new SerializedPropertyPathBuilder();
            PrefabModification? prefabModification = null;
            int lineNumber = 0;

            using var reader = new StreamReader(
                absolutePath,
                Encoding.UTF8,
                true,
                ReadBufferSize);
            while (reader.ReadLine() is { } line)
            {
                lineNumber++;
                if (TryParseDocumentHeader(line, out long documentLocalId))
                {
                    document = new SerializedDocument(documentLocalId);
                    model.Documents[documentLocalId] = document;
                    propertyPath.Reset();
                    prefabModification = null;
                    continue;
                }

                if (document == null)
                {
                    continue;
                }

                int indentation = CountLeadingSpaces(line);
                string trimmedLine = line.Substring(indentation);
                if (indentation == 0 &&
                    TryParseMapping(trimmedLine, out string rootKey, out _, out bool rootSequenceItem))
                {
                    if (!rootSequenceItem && document.RootType.Length == 0)
                    {
                        document.RootType = rootKey;
                        propertyPath.Reset();
                    }

                    continue;
                }

                if (!TryParseMapping(
                        trimmedLine,
                        out string key,
                        out string scalarValue,
                        out bool isSequenceItem))
                {
                    continue;
                }

                if (indentation == 2 && !isSequenceItem)
                {
                    ReadDocumentMetadata(document, key, scalarValue);
                }

                if (document.RootType == "PrefabInstance")
                {
                    if (isSequenceItem && key == "target")
                    {
                        prefabModification = new PrefabModification(
                            ParseFileId(scalarValue),
                            ParseGuid(scalarValue));
                    }
                    else if (prefabModification != null && key == "propertyPath")
                    {
                        prefabModification.PropertyPath = TrimYamlScalar(scalarValue);
                    }
                    else if (prefabModification != null && key == "value" &&
                             prefabModification.PropertyPath.IndexOf(
                                 EntryIdPropertyName,
                                 StringComparison.Ordinal) >= 0)
                    {
                        AddCandidate(
                            model,
                            document,
                            prefabModification.PropertyPath,
                            scalarValue,
                            lineNumber,
                            true,
                            prefabModification.TargetAssetGuid,
                            prefabModification.TargetLocalId,
                            warnings);
                    }
                }

                string currentPropertyPath = propertyPath.Process(
                    indentation,
                    key,
                    scalarValue,
                    isSequenceItem);
                if (key == EntryIdPropertyName)
                {
                    model.KeyDocumentLocalIds.Add(document.LocalId);
                    AddCandidate(
                        model,
                        document,
                        currentPropertyPath,
                        scalarValue,
                        lineNumber,
                        false,
                        string.Empty,
                        0,
                        warnings);
                }
            }

            return model;
        }

        private static void AddCandidate(
            SerializedAssetModel model,
            SerializedDocument document,
            string propertyPath,
            string scalarValue,
            int line,
            bool isPrefabOverride,
            string targetAssetGuid,
            long targetLocalId,
            ICollection<string> warnings)
        {
            if (!long.TryParse(
                    TrimYamlScalar(scalarValue),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out long entryId))
            {
                warnings.Add(
                    $"Invalid I18nKey value at {model.AssetPath}:{line}: '{TrimYamlScalar(scalarValue)}'.");
                return;
            }

            if (entryId == 0)
            {
                return;
            }

            if (!I18nEntryId.IsValid(entryId))
            {
                warnings.Add($"Invalid I18nKey ID {entryId} at {model.AssetPath}:{line}.");
                return;
            }

            model.Candidates.Add(new SerializedUsageCandidate(
                entryId,
                document.LocalId,
                propertyPath,
                line,
                isPrefabOverride,
                targetAssetGuid,
                targetLocalId));
        }

        private static void ReadDocumentMetadata(
            SerializedDocument document,
            string key,
            string scalarValue)
        {
            switch (key)
            {
                case "m_GameObject":
                    document.GameObjectLocalId = ParseFileId(scalarValue);
                    break;
                case "m_Script":
                    document.ScriptGuid = ParseGuid(scalarValue);
                    break;
                case "m_Name":
                    document.Name = TrimYamlScalar(scalarValue);
                    break;
                case "m_EditorClassIdentifier":
                    document.EditorClassIdentifier = TrimYamlScalar(scalarValue);
                    break;
                case "m_Father":
                    document.ParentTransformLocalId = ParseFileId(scalarValue);
                    break;
            }
        }

        private static bool TryParseDocumentHeader(string line, out long localId)
        {
            localId = 0;
            if (!line.StartsWith("--- !u!", StringComparison.Ordinal))
            {
                return false;
            }

            int ampersandIndex = line.IndexOf('&');
            if (ampersandIndex < 0)
            {
                return false;
            }

            int endIndex = ampersandIndex + 1;
            while (endIndex < line.Length &&
                   (char.IsDigit(line[endIndex]) || line[endIndex] == '-'))
            {
                endIndex++;
            }

            return long.TryParse(
                line.Substring(ampersandIndex + 1, endIndex - ampersandIndex - 1),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out localId);
        }

        private static bool TryParseMapping(
            string line,
            out string key,
            out string scalarValue,
            out bool isSequenceItem)
        {
            key = string.Empty;
            scalarValue = string.Empty;
            isSequenceItem = line.StartsWith("- ", StringComparison.Ordinal);
            string mapping = isSequenceItem ? line.Substring(2) : line;
            int colonIndex = mapping.IndexOf(':');
            if (colonIndex <= 0)
            {
                return false;
            }

            key = mapping.Substring(0, colonIndex).Trim();
            scalarValue = mapping.Substring(colonIndex + 1).Trim();
            return key.Length > 0;
        }

        private static int CountLeadingSpaces(string value)
        {
            int count = 0;
            while (count < value.Length && value[count] == ' ')
            {
                count++;
            }

            return count;
        }

        private static long ParseFileId(string value)
        {
            const string prefix = "fileID:";
            int startIndex = value.IndexOf(prefix, StringComparison.Ordinal);
            if (startIndex < 0)
            {
                return 0;
            }

            startIndex += prefix.Length;
            while (startIndex < value.Length && value[startIndex] == ' ')
            {
                startIndex++;
            }

            int endIndex = startIndex;
            while (endIndex < value.Length &&
                   (char.IsDigit(value[endIndex]) || value[endIndex] == '-'))
            {
                endIndex++;
            }

            return long.TryParse(
                value.Substring(startIndex, endIndex - startIndex),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long fileId)
                ? fileId
                : 0;
        }

        private static string ParseGuid(string value)
        {
            const string prefix = "guid:";
            int startIndex = value.IndexOf(prefix, StringComparison.Ordinal);
            if (startIndex < 0)
            {
                return string.Empty;
            }

            startIndex += prefix.Length;
            while (startIndex < value.Length && value[startIndex] == ' ')
            {
                startIndex++;
            }

            int endIndex = startIndex;
            while (endIndex < value.Length &&
                   (char.IsLetterOrDigit(value[endIndex]) || value[endIndex] == '-'))
            {
                endIndex++;
            }

            return value.Substring(startIndex, endIndex - startIndex);
        }

        private static string TrimYamlScalar(string value)
        {
            string trimmed = value.Trim();
            if (trimmed.Length >= 2 &&
                ((trimmed[0] == '\'' && trimmed[trimmed.Length - 1] == '\'') ||
                 (trimmed[0] == '"' && trimmed[trimmed.Length - 1] == '"')))
            {
                return trimmed.Substring(1, trimmed.Length - 2);
            }

            return trimmed;
        }

        private static string ToAssetPath(string absolutePath, string projectRoot)
        {
            string normalizedPath = Path.GetFullPath(absolutePath).Replace('\\', '/');
            string normalizedRoot = Path.GetFullPath(projectRoot).Replace('\\', '/').TrimEnd('/');
            return normalizedPath.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase)
                ? normalizedPath.Substring(normalizedRoot.Length + 1)
                : normalizedPath;
        }

        private static string FormatLocation(I18nAssetUsage usage)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string absolutePath = Path.GetFullPath(Path.Combine(projectRoot, usage.AssetPath));
            string href = EscapeRichText(absolutePath);
            string label = EscapeRichText($"{usage.AssetPath}:{usage.Line}");
            string context = EscapeRichText(usage.FormatContext());
            return $"  <color=#40a0ff><a href=\"{href}\" line=\"{usage.Line}\">{label}</a></color> - {context}";
        }

        private static string EscapeRichText(string value)
        {
            return value
                .Replace("&", "&amp;")
                .Replace("\"", "&quot;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        private static string FormatByteCount(long byteCount)
        {
            if (byteCount < 1024)
            {
                return $"{byteCount} B";
            }

            if (byteCount < 1024 * 1024)
            {
                return $"{byteCount / 1024d:0.0} KiB";
            }

            return $"{byteCount / (1024d * 1024d):0.0} MiB";
        }

        private sealed class SerializedPropertyPathBuilder
        {
            private readonly List<PathNode> _nodes = new();

            public void Reset()
            {
                _nodes.Clear();
            }

            public string Process(
                int indentation,
                string key,
                string scalarValue,
                bool isSequenceItem)
            {
                if (isSequenceItem)
                {
                    while (_nodes.Count > 0 && _nodes[_nodes.Count - 1].Indentation > indentation)
                    {
                        _nodes.RemoveAt(_nodes.Count - 1);
                    }

                    if (_nodes.Count > 0)
                    {
                        PathNode parent = _nodes[_nodes.Count - 1];
                        int itemIndex = parent.NextSequenceIndex++;
                        _nodes.Add(new PathNode(indentation + 1, $"Array.data[{itemIndex}]"));
                    }
                }
                else
                {
                    while (_nodes.Count > 0 && _nodes[_nodes.Count - 1].Indentation >= indentation)
                    {
                        _nodes.RemoveAt(_nodes.Count - 1);
                    }
                }

                string path = string.Join(".", _nodes.Select(node => node.Name).Append(key));
                if (scalarValue.Length == 0 && key != EntryIdPropertyName)
                {
                    _nodes.Add(new PathNode(isSequenceItem ? indentation + 1 : indentation, key));
                }

                return path;
            }

            private sealed class PathNode
            {
                public PathNode(int indentation, string name)
                {
                    Indentation = indentation;
                    Name = name;
                }

                public int Indentation { get; }

                public string Name { get; }

                public int NextSequenceIndex { get; set; }
            }
        }

        private sealed class PrefabModification
        {
            public PrefabModification(long targetLocalId, string targetAssetGuid)
            {
                TargetLocalId = targetLocalId;
                TargetAssetGuid = targetAssetGuid;
                PropertyPath = string.Empty;
            }

            public long TargetLocalId { get; }

            public string TargetAssetGuid { get; }

            public string PropertyPath { get; set; }
        }

        private sealed class ScriptTypeResolver
        {
            private readonly Dictionary<string, string> _typesByGuid = new(StringComparer.Ordinal);

            public string Resolve(string scriptGuid)
            {
                if (scriptGuid.Length == 0)
                {
                    return string.Empty;
                }

                if (_typesByGuid.TryGetValue(scriptGuid, out string typeName))
                {
                    return typeName;
                }

                string scriptPath = AssetDatabase.GUIDToAssetPath(scriptGuid);
                typeName = scriptPath.Length > 0
                    ? Path.GetFileNameWithoutExtension(scriptPath)
                    : string.Empty;
                _typesByGuid.Add(scriptGuid, typeName);
                return typeName;
            }
        }

        private sealed class SerializedAssetModel
        {
            private readonly Dictionary<long, SerializedObjectContext> _contexts = new();
            private IReadOnlyDictionary<long, SerializedDocument>? _gameObjects;
            private IReadOnlyDictionary<long, SerializedDocument>? _transforms;
            private IReadOnlyDictionary<long, long>? _transformsByGameObject;

            public SerializedAssetModel(string assetPath, string assetGuid)
            {
                AssetPath = assetPath;
                AssetGuid = assetGuid;
            }

            public string AssetPath { get; }

            public string AssetGuid { get; }

            public Dictionary<long, SerializedDocument> Documents { get; } = new();

            public List<SerializedUsageCandidate> Candidates { get; } = new();

            public HashSet<long> KeyDocumentLocalIds { get; } = new();

            public void PrepareContexts(ScriptTypeResolver scriptTypeResolver)
            {
                foreach (long documentLocalId in KeyDocumentLocalIds)
                {
                    ResolveContext(documentLocalId, scriptTypeResolver);
                }

                Documents.Clear();
                _gameObjects = null;
                _transforms = null;
                _transformsByGameObject = null;
            }

            public IEnumerable<I18nAssetUsage> CreateUsages(
                IReadOnlyDictionary<string, SerializedAssetModel> modelsByGuid,
                ScriptTypeResolver scriptTypeResolver)
            {
                foreach (SerializedUsageCandidate candidate in Candidates)
                {
                    SerializedAssetModel contextModel = this;
                    long contextDocumentLocalId = candidate.DocumentLocalId;
                    string targetAssetPath = string.Empty;
                    if (candidate.IsPrefabOverride &&
                        candidate.TargetAssetGuid.Length > 0 &&
                        modelsByGuid.TryGetValue(candidate.TargetAssetGuid, out SerializedAssetModel targetModel))
                    {
                        contextModel = targetModel;
                        contextDocumentLocalId = candidate.TargetLocalId;
                        targetAssetPath = targetModel.AssetPath;
                    }

                    SerializedObjectContext context = contextModel.ResolveContext(
                        contextDocumentLocalId,
                        scriptTypeResolver);
                    yield return new I18nAssetUsage(
                        candidate.EntryId,
                        AssetGuid,
                        AssetPath,
                        candidate.DocumentLocalId,
                        context.GameObjectLocalId,
                        context.ObjectPath,
                        context.ComponentType,
                        candidate.PropertyPath,
                        candidate.Line,
                        candidate.IsPrefabOverride,
                        candidate.TargetAssetGuid,
                        targetAssetPath,
                        candidate.TargetLocalId);
                }
            }

            private SerializedObjectContext ResolveContext(
                long documentLocalId,
                ScriptTypeResolver scriptTypeResolver)
            {
                if (_contexts.TryGetValue(documentLocalId, out SerializedObjectContext cachedContext))
                {
                    return cachedContext;
                }

                if (!Documents.TryGetValue(documentLocalId, out SerializedDocument document))
                {
                    return SerializedObjectContext.Empty;
                }

                string componentType = ResolveComponentType(document, scriptTypeResolver);
                SerializedObjectContext context;
                if (document.GameObjectLocalId == 0)
                {
                    string objectName = document.Name.Length > 0
                        ? document.Name
                        : Path.GetFileNameWithoutExtension(AssetPath);
                    context = new SerializedObjectContext(0, objectName, componentType);
                }
                else
                {
                    context = new SerializedObjectContext(
                        document.GameObjectLocalId,
                        BuildHierarchyPath(document.GameObjectLocalId),
                        componentType);
                }

                _contexts.Add(documentLocalId, context);
                return context;
            }

            private string ResolveComponentType(
                SerializedDocument document,
                ScriptTypeResolver scriptTypeResolver)
            {
                if (document.EditorClassIdentifier.Length > 0)
                {
                    int separatorIndex = document.EditorClassIdentifier.LastIndexOf("::", StringComparison.Ordinal);
                    string typeName = separatorIndex >= 0
                        ? document.EditorClassIdentifier.Substring(separatorIndex + 2)
                        : document.EditorClassIdentifier;
                    int namespaceIndex = typeName.LastIndexOf('.');
                    return namespaceIndex >= 0 ? typeName.Substring(namespaceIndex + 1) : typeName;
                }

                string scriptType = scriptTypeResolver.Resolve(document.ScriptGuid);
                return scriptType.Length > 0 ? scriptType : document.RootType;
            }

            private string BuildHierarchyPath(long gameObjectLocalId)
            {
                EnsureContextIndexes();

                var names = new Stack<string>();
                var visitedTransforms = new HashSet<long>();
                long currentGameObjectLocalId = gameObjectLocalId;
                while (currentGameObjectLocalId != 0)
                {
                    names.Push(
                        _gameObjects!.TryGetValue(currentGameObjectLocalId, out SerializedDocument gameObject) &&
                        gameObject.Name.Length > 0
                            ? gameObject.Name
                            : $"GameObject {currentGameObjectLocalId}");

                    if (!_transformsByGameObject!.TryGetValue(currentGameObjectLocalId, out long transformLocalId) ||
                        !visitedTransforms.Add(transformLocalId) ||
                        !_transforms!.TryGetValue(transformLocalId, out SerializedDocument transform) ||
                        transform.ParentTransformLocalId == 0 ||
                        !_transforms.TryGetValue(
                            transform.ParentTransformLocalId,
                            out SerializedDocument parentTransform))
                    {
                        break;
                    }

                    currentGameObjectLocalId = parentTransform.GameObjectLocalId;
                }

                return string.Join(" / ", names);
            }

            private void EnsureContextIndexes()
            {
                if (_gameObjects != null)
                {
                    return;
                }

                _gameObjects = Documents.Values
                    .Where(document => document.RootType == "GameObject")
                    .ToDictionary(document => document.LocalId);
                _transforms = Documents.Values
                    .Where(document => document.GameObjectLocalId != 0 &&
                                       (document.RootType == "Transform" || document.RootType == "RectTransform"))
                    .ToDictionary(document => document.LocalId);
                _transformsByGameObject = _transforms.Values
                    .GroupBy(transform => transform.GameObjectLocalId)
                    .ToDictionary(group => group.Key, group => group.First().LocalId);
            }
        }

        private sealed class SerializedDocument
        {
            public SerializedDocument(long localId)
            {
                LocalId = localId;
            }

            public long LocalId { get; }

            public string RootType { get; set; } = string.Empty;

            public long GameObjectLocalId { get; set; }

            public long ParentTransformLocalId { get; set; }

            public string ScriptGuid { get; set; } = string.Empty;

            public string Name { get; set; } = string.Empty;

            public string EditorClassIdentifier { get; set; } = string.Empty;
        }

        private sealed class SerializedUsageCandidate
        {
            public SerializedUsageCandidate(
                long entryId,
                long documentLocalId,
                string propertyPath,
                int line,
                bool isPrefabOverride,
                string targetAssetGuid,
                long targetLocalId)
            {
                EntryId = entryId;
                DocumentLocalId = documentLocalId;
                PropertyPath = propertyPath;
                Line = line;
                IsPrefabOverride = isPrefabOverride;
                TargetAssetGuid = targetAssetGuid;
                TargetLocalId = targetLocalId;
            }

            public long EntryId { get; }

            public long DocumentLocalId { get; }

            public string PropertyPath { get; }

            public int Line { get; }

            public bool IsPrefabOverride { get; }

            public string TargetAssetGuid { get; }

            public long TargetLocalId { get; }
        }

        private sealed class SerializedObjectContext
        {
            public static SerializedObjectContext Empty { get; } = new(0, string.Empty, string.Empty);

            public SerializedObjectContext(
                long gameObjectLocalId,
                string objectPath,
                string componentType)
            {
                GameObjectLocalId = gameObjectLocalId;
                ObjectPath = objectPath;
                ComponentType = componentType;
            }

            public long GameObjectLocalId { get; }

            public string ObjectPath { get; }

            public string ComponentType { get; }
        }
    }

    internal sealed class I18nAssetUsageScanResult
    {
        public I18nAssetUsageScanResult(
            IReadOnlyList<I18nAssetUsage> usages,
            IReadOnlyList<string> warnings,
            int scannedAssetCount,
            int matchedAssetCount,
            long scannedByteCount,
            I18nUsageScanPerformance performance,
            bool isForceText)
        {
            Usages = usages;
            Warnings = warnings;
            ScannedAssetCount = scannedAssetCount;
            MatchedAssetCount = matchedAssetCount;
            ScannedByteCount = scannedByteCount;
            Performance = performance;
            IsForceText = isForceText;
        }

        public IReadOnlyList<I18nAssetUsage> Usages { get; }

        public IReadOnlyList<string> Warnings { get; }

        public int ScannedAssetCount { get; }

        public int MatchedAssetCount { get; }

        public long ScannedByteCount { get; }

        public I18nUsageScanPerformance Performance { get; }

        public long ElapsedMilliseconds => Performance.ElapsedMilliseconds;

        public bool IsForceText { get; }
    }

    internal sealed class I18nAssetUsage
    {
        public I18nAssetUsage(
            long entryId,
            string assetGuid,
            string assetPath,
            long assetLocalId,
            long gameObjectLocalId,
            string objectPath,
            string componentType,
            string propertyPath,
            int line,
            bool isPrefabOverride,
            string targetAssetGuid,
            string targetAssetPath,
            long targetLocalId)
        {
            EntryId = entryId;
            AssetGuid = assetGuid;
            AssetPath = assetPath;
            AssetLocalId = assetLocalId;
            GameObjectLocalId = gameObjectLocalId;
            ObjectPath = objectPath;
            ComponentType = componentType;
            PropertyPath = propertyPath;
            Line = line;
            IsPrefabOverride = isPrefabOverride;
            TargetAssetGuid = targetAssetGuid;
            TargetAssetPath = targetAssetPath;
            TargetLocalId = targetLocalId;
        }

        public long EntryId { get; }

        public string AssetGuid { get; }

        public string AssetPath { get; }

        public long AssetLocalId { get; }

        public long GameObjectLocalId { get; }

        public string ObjectPath { get; }

        public string ComponentType { get; }

        public string PropertyPath { get; }

        public int Line { get; }

        public bool IsPrefabOverride { get; }

        public string TargetAssetGuid { get; }

        public string TargetAssetPath { get; }

        public long TargetLocalId { get; }

        public string FormatContext()
        {
            var parts = new List<string>();
            if (IsPrefabOverride)
            {
                parts.Add(TargetAssetPath.Length > 0
                    ? $"Prefab override -> {TargetAssetPath}"
                    : "Prefab override");
            }

            if (ObjectPath.Length > 0)
            {
                parts.Add(ObjectPath);
            }

            if (ComponentType.Length > 0)
            {
                parts.Add(ComponentType);
            }

            parts.Add(PropertyPath);
            return string.Join(" / ", parts);
        }
    }
}
