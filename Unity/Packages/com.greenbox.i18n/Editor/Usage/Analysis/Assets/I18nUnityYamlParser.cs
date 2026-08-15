#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GreenBox.I18n.Usage.Analysis
{
    internal static class I18nUnityYamlParser
    {
        private const string EntryIdPropertyName = "_greenBoxI18nEntryId";
        private const int ReadBufferSize = 64 * 1024;

        internal static I18nSerializedAssetModel Parse(
            string absolutePath,
            string assetPath,
            string assetGuid,
            ICollection<string> warnings)
        {
            var model = new I18nSerializedAssetModel(assetPath, assetGuid);
            I18nSerializedDocument? document = null;
            var propertyPath = new I18nSerializedPropertyPathBuilder();
            PrefabModification? prefabModification = null;
            OdinEntryIdNode? odinEntryIdNode = null;
            int lineNumber = 0;

            using var reader = new StreamReader(absolutePath, Encoding.UTF8, true, ReadBufferSize);
            while (reader.ReadLine() is { } line)
            {
                lineNumber++;
                if (TryParseDocumentHeader(line, out long documentLocalId))
                {
                    document = new I18nSerializedDocument(documentLocalId);
                    model.Documents[documentLocalId] = document;
                    propertyPath.Reset();
                    prefabModification = null;
                    odinEntryIdNode = null;
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

                if (odinEntryIdNode != null && indentation <= odinEntryIdNode.SequenceIndentation)
                {
                    odinEntryIdNode = null;
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
                else if (isSequenceItem &&
                         key == "Name" &&
                         TrimYamlScalar(scalarValue) == EntryIdPropertyName &&
                         IsOdinSerializationNodePath(currentPropertyPath))
                {
                    odinEntryIdNode = new OdinEntryIdNode(indentation);
                }
                else if (odinEntryIdNode != null &&
                         !isSequenceItem &&
                         indentation > odinEntryIdNode.SequenceIndentation &&
                         key == "Data")
                {
                    model.KeyDocumentLocalIds.Add(document.LocalId);
                    AddCandidate(
                        model,
                        document,
                        ReplacePropertyPathLeaf(currentPropertyPath, key, EntryIdPropertyName),
                        scalarValue,
                        lineNumber,
                        false,
                        string.Empty,
                        0,
                        warnings);
                    odinEntryIdNode = null;
                }
            }

            return model;
        }

        private static bool IsOdinSerializationNodePath(string propertyPath)
        {
            return propertyPath.IndexOf(
                       ".SerializationNodes.Array.data[",
                       StringComparison.Ordinal) >= 0;
        }

        private static string ReplacePropertyPathLeaf(
            string propertyPath,
            string currentLeaf,
            string replacementLeaf)
        {
            return propertyPath.EndsWith(currentLeaf, StringComparison.Ordinal)
                ? propertyPath.Substring(0, propertyPath.Length - currentLeaf.Length) + replacementLeaf
                : propertyPath + "." + replacementLeaf;
        }

        private static void AddCandidate(
            I18nSerializedAssetModel model,
            I18nSerializedDocument document,
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

            model.Candidates.Add(new I18nSerializedUsageCandidate(
                entryId,
                document.LocalId,
                propertyPath,
                line,
                isPrefabOverride,
                targetAssetGuid,
                targetLocalId));
        }

        private static void ReadDocumentMetadata(
            I18nSerializedDocument document,
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

        private sealed class OdinEntryIdNode
        {
            public OdinEntryIdNode(int sequenceIndentation)
            {
                SequenceIndentation = sequenceIndentation;
            }

            public int SequenceIndentation { get; }
        }
    }
}
