#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GreenBox.I18n.Usage.Analysis
{
    internal sealed class I18nSerializedAssetModel
    {
            private readonly Dictionary<long, I18nSerializedObjectContext> _contexts = new();
            private IReadOnlyDictionary<long, I18nSerializedDocument>? _gameObjects;
            private IReadOnlyDictionary<long, I18nSerializedDocument>? _transforms;
            private IReadOnlyDictionary<long, long>? _transformsByGameObject;

            public I18nSerializedAssetModel(string assetPath, string assetGuid)
            {
                AssetPath = assetPath;
                AssetGuid = assetGuid;
            }

            public string AssetPath { get; }
            public string AssetGuid { get; }
            public Dictionary<long, I18nSerializedDocument> Documents { get; } = new();
            public List<I18nSerializedUsageCandidate> Candidates { get; } = new();
            public HashSet<long> KeyDocumentLocalIds { get; } = new();

            public void PrepareContexts()
            {
                foreach (long documentLocalId in KeyDocumentLocalIds)
                {
                    ResolveContext(documentLocalId);
                }

                Documents.Clear();
                _gameObjects = null;
                _transforms = null;
                _transformsByGameObject = null;
            }

            public IEnumerable<I18nAssetUsage> CreateUsages(
                IReadOnlyDictionary<string, I18nSerializedAssetModel> modelsByGuid)
            {
                foreach (I18nSerializedUsageCandidate candidate in Candidates)
                {
                    I18nSerializedAssetModel contextModel = this;
                    long contextDocumentLocalId = candidate.DocumentLocalId;
                    string targetAssetPath = string.Empty;
                    if (candidate.IsPrefabOverride &&
                        candidate.TargetAssetGuid.Length > 0 &&
                        modelsByGuid.TryGetValue(
                            candidate.TargetAssetGuid,
                            out I18nSerializedAssetModel targetModel))
                    {
                        contextModel = targetModel;
                        contextDocumentLocalId = candidate.TargetLocalId;
                        targetAssetPath = targetModel.AssetPath;
                    }

                    I18nSerializedObjectContext context = contextModel.ResolveContext(contextDocumentLocalId);
                    yield return new I18nAssetUsage(
                        candidate.EntryId,
                        AssetGuid,
                        AssetPath,
                        candidate.DocumentLocalId,
                        context.GameObjectLocalId,
                        context.ObjectPath,
                        context.ComponentType,
                        context.ScriptGuid,
                        candidate.PropertyPath,
                        candidate.Line,
                        candidate.IsPrefabOverride,
                        candidate.TargetAssetGuid,
                        targetAssetPath,
                        candidate.TargetLocalId);
                }
            }

            private I18nSerializedObjectContext ResolveContext(long documentLocalId)
            {
                if (_contexts.TryGetValue(documentLocalId, out I18nSerializedObjectContext cachedContext))
                {
                    return cachedContext;
                }

                if (!Documents.TryGetValue(documentLocalId, out I18nSerializedDocument document))
                {
                    return I18nSerializedObjectContext.Empty;
                }

                string componentType = ResolveComponentType(document);
                I18nSerializedObjectContext context;
                if (document.GameObjectLocalId == 0)
                {
                    string objectName = document.Name.Length > 0
                        ? document.Name
                        : Path.GetFileNameWithoutExtension(AssetPath);
                    context = new I18nSerializedObjectContext(
                        0,
                        objectName,
                        componentType,
                        document.ScriptGuid);
                }
                else
                {
                    context = new I18nSerializedObjectContext(
                        document.GameObjectLocalId,
                        BuildHierarchyPath(document.GameObjectLocalId),
                        componentType,
                        document.ScriptGuid);
                }

                _contexts.Add(documentLocalId, context);
                return context;
            }

            private static string ResolveComponentType(I18nSerializedDocument document)
            {
                if (document.EditorClassIdentifier.Length == 0)
                {
                    return document.RootType;
                }

                int separatorIndex = document.EditorClassIdentifier.LastIndexOf("::", StringComparison.Ordinal);
                string typeName = separatorIndex >= 0
                    ? document.EditorClassIdentifier.Substring(separatorIndex + 2)
                    : document.EditorClassIdentifier;
                int namespaceIndex = typeName.LastIndexOf('.');
                return namespaceIndex >= 0 ? typeName.Substring(namespaceIndex + 1) : typeName;
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
                        _gameObjects!.TryGetValue(
                            currentGameObjectLocalId,
                            out I18nSerializedDocument gameObject) &&
                        gameObject.Name.Length > 0
                            ? gameObject.Name
                            : $"GameObject {currentGameObjectLocalId}");

                    if (!_transformsByGameObject!.TryGetValue(currentGameObjectLocalId, out long transformLocalId) ||
                        !visitedTransforms.Add(transformLocalId) ||
                        !_transforms!.TryGetValue(transformLocalId, out I18nSerializedDocument transform) ||
                        transform.ParentTransformLocalId == 0 ||
                        !_transforms.TryGetValue(
                            transform.ParentTransformLocalId,
                            out I18nSerializedDocument parentTransform))
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

    internal sealed class I18nSerializedDocument
        {
            public I18nSerializedDocument(long localId) => LocalId = localId;
            public long LocalId { get; }
            public string RootType { get; set; } = string.Empty;
            public long GameObjectLocalId { get; set; }
            public long ParentTransformLocalId { get; set; }
            public string ScriptGuid { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string EditorClassIdentifier { get; set; } = string.Empty;
        }

    internal sealed class I18nSerializedUsageCandidate
        {
            public I18nSerializedUsageCandidate(
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

    internal sealed class I18nSerializedObjectContext
        {
            public static I18nSerializedObjectContext Empty { get; } = new(
                0, string.Empty, string.Empty, string.Empty);

            public I18nSerializedObjectContext(
                long gameObjectLocalId,
                string objectPath,
                string componentType,
                string scriptGuid)
            {
                GameObjectLocalId = gameObjectLocalId;
                ObjectPath = objectPath;
                ComponentType = componentType;
                ScriptGuid = scriptGuid;
            }

            public long GameObjectLocalId { get; }
            public string ObjectPath { get; }
            public string ComponentType { get; }
            public string ScriptGuid { get; }
        }
}
