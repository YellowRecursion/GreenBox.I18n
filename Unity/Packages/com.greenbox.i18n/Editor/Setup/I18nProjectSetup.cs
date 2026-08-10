#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GreenBox.I18n.Unity.Editor.Compilation;
using GreenBox.I18n.Unity.Editor.Settings;
using UnityEditor;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Setup
{
    /// <summary>
    /// Creates and repairs the source-controlled project files and generated runtime catalog.
    /// </summary>
    internal static class I18nProjectSetup
    {
        internal static bool IsHealthy()
        {
            I18nProjectSettings settings = I18nProjectSettings.instance;
            TextAsset? sourceCatalog = settings.SourceCatalog;
            I18nCatalogAsset? projectCatalog = settings.ProjectCatalog;
            if (!settings.IsSetupComplete ||
                !sourceCatalog ||
                !projectCatalog ||
                projectCatalog.SourceCatalog != sourceCatalog)
            {
                return false;
            }

            string sourceFolder = GetParentPath(AssetDatabase.GetAssetPath(sourceCatalog));
            return
                File.Exists(GetAbsoluteProjectPath(sourceFolder + "/readme.md")) &&
                File.Exists(GetAbsoluteProjectPath(sourceFolder + "/.gitignore")) &&
                File.Exists(GetAbsoluteProjectPath(sourceFolder + "/.gitattributes")) &&
                AssetDatabase.IsValidFolder(
                    I18nProjectLayout.GetResourcesFolderPath(
                        AssetDatabase.GetAssetPath(sourceCatalog)));
        }

        internal static bool TryRepair(out string error)
        {
            try
            {
                I18nProjectSettings settings = I18nProjectSettings.instance;
                TextAsset? sourceCatalog = settings.SourceCatalog ?? FindExistingSourceCatalog();
                if (!sourceCatalog)
                {
                    if (settings.IsSetupComplete ||
                        !string.IsNullOrEmpty(settings.SourceCatalogPath))
                    {
                        error =
                            "The localization source JSON is missing. Restore localization.json " +
                            "from version control before continuing.";
                        return false;
                    }

                    sourceCatalog = CreateDefaultSourceFiles();
                }

                EnsureManagedCompanionFiles(sourceCatalog);
                settings.ConfigureSourceCatalog(sourceCatalog);

                I18nCatalogAsset catalogAsset = EnsureGeneratedCatalog(sourceCatalog);
                I18nCatalogAutoCompiler.Queue(catalogAsset);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static TextAsset? FindExistingSourceCatalog()
        {
            TextAsset? defaultSource =
                AssetDatabase.LoadAssetAtPath<TextAsset>(I18nProjectLayout.SourceCatalogPath);
            if (defaultSource)
            {
                return defaultSource;
            }

            string[] sourceGuids = AssetDatabase.FindAssets("localization t:TextAsset", new[] { "Assets" });
            var matches = new List<TextAsset>();
            for (int sourceIndex = 0; sourceIndex < sourceGuids.Length; sourceIndex++)
            {
                string sourcePath = AssetDatabase.GUIDToAssetPath(sourceGuids[sourceIndex]);
                if (!sourcePath.EndsWith("/localization.json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                TextAsset? source = AssetDatabase.LoadAssetAtPath<TextAsset>(sourcePath);
                if (source)
                {
                    matches.Add(source);
                }
            }

            if (matches.Count > 1)
            {
                throw new InvalidOperationException(
                    "Multiple localization.json files were found. GreenBox I18n supports one " +
                    "project catalog per Unity project.");
            }

            return matches.Count == 1 ? matches[0] : null;
        }

        private static TextAsset CreateDefaultSourceFiles()
        {
            EnsureFolder("Assets", "GreenBox.I18n");
            WriteFileIfMissing(
                I18nProjectLayout.SourceCatalogPath,
                I18nProjectLayout.CreateInitialCatalogJson());
            AssetDatabase.ImportAsset(
                I18nProjectLayout.SourceCatalogPath,
                ImportAssetOptions.ForceSynchronousImport);

            return AssetDatabase.LoadAssetAtPath<TextAsset>(I18nProjectLayout.SourceCatalogPath)
                ?? throw new InvalidOperationException(
                    $"Unity could not import '{I18nProjectLayout.SourceCatalogPath}' as a TextAsset.");
        }

        private static void EnsureManagedCompanionFiles(TextAsset sourceCatalog)
        {
            string sourcePath = AssetDatabase.GetAssetPath(sourceCatalog);
            string sourceFolder = GetParentPath(sourcePath);
            string readmePath = sourceFolder + "/readme.md";
            string gitIgnorePath = sourceFolder + "/.gitignore";
            string gitAttributesPath = sourceFolder + "/.gitattributes";

            WriteFileIfMissing(readmePath, I18nProjectLayout.CreateReadme());
            WriteFileIfMissing(gitIgnorePath, I18nProjectLayout.CreateGitIgnore());
            WriteFileIfMissing(
                gitAttributesPath,
                I18nProjectLayout.CreateGitAttributes());
            AssetDatabase.ImportAsset(readmePath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static I18nCatalogAsset EnsureGeneratedCatalog(TextAsset sourceCatalog)
        {
            string sourcePath = AssetDatabase.GetAssetPath(sourceCatalog);
            string resourcesPath = I18nProjectLayout.GetResourcesFolderPath(sourcePath);
            string catalogPath = I18nProjectLayout.GetCatalogAssetPath(sourcePath);
            EnsureFolder(GetParentPath(resourcesPath), "Resources");

            UnityEngine.Object? occupiedAsset = AssetDatabase.LoadMainAssetAtPath(catalogPath);
            if (occupiedAsset && occupiedAsset is not I18nCatalogAsset)
            {
                throw new InvalidOperationException(
                    $"Cannot generate the runtime catalog because '{catalogPath}' is occupied " +
                    "by another asset.");
            }

            I18nCatalogAsset? catalogAsset = occupiedAsset as I18nCatalogAsset;
            if (!catalogAsset)
            {
                catalogAsset = ScriptableObject.CreateInstance<I18nCatalogAsset>();
                catalogAsset.name = I18nCatalogAsset.ResourcesPath;
                AssetDatabase.CreateAsset(catalogAsset, catalogPath);
            }

            if (catalogAsset.SourceCatalog != sourceCatalog)
            {
                AssignSourceCatalog(catalogAsset, sourceCatalog);
            }

            return catalogAsset;
        }

        private static void EnsureFolder(string parentPath, string folderName)
        {
            string folderPath = parentPath + "/" + folderName;
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string folderGuid = AssetDatabase.CreateFolder(parentPath, folderName);
            if (string.IsNullOrEmpty(folderGuid))
            {
                throw new IOException($"Could not create the folder '{folderPath}'.");
            }
        }

        private static void WriteFileIfMissing(string assetPath, string contents)
        {
            string absolutePath = GetAbsoluteProjectPath(assetPath);
            if (File.Exists(absolutePath))
            {
                return;
            }

            File.WriteAllText(absolutePath, contents, new UTF8Encoding(false));
        }

        private static string GetAbsoluteProjectPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Unity project root could not be resolved.");
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private static string GetParentPath(string assetPath)
        {
            string? parentPath = Path.GetDirectoryName(assetPath);
            return string.IsNullOrEmpty(parentPath)
                ? throw new ArgumentException("Unity asset path has no parent folder.", nameof(assetPath))
                : parentPath.Replace('\\', '/');
        }

        private static void AssignSourceCatalog(
            I18nCatalogAsset catalogAsset,
            TextAsset sourceCatalog)
        {
            var serializedCatalog = new SerializedObject(catalogAsset);
            SerializedProperty? sourceProperty = serializedCatalog.FindProperty("_sourceCatalog");
            if (sourceProperty == null)
            {
                throw new InvalidOperationException(
                    "The GreenBox I18n catalog source property could not be found.");
            }

            sourceProperty.objectReferenceValue = sourceCatalog;
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalogAsset);
            AssetDatabase.SaveAssetIfDirty(catalogAsset);
        }
    }
}
