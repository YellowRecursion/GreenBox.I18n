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
    /// Creates or adopts the single project catalog used by GreenBox I18n tooling.
    /// </summary>
    internal static class I18nProjectSetup
    {
        internal static bool TryRepair(out string error)
        {
            try
            {
                I18nProjectSettings settings = I18nProjectSettings.instance;
                I18nCatalogAsset? activeCatalog = settings.ActiveCatalog;
                if (activeCatalog && activeCatalog.SourceCatalog)
                {
                    activeCatalog = EnsureRuntimeCatalogLocation(activeCatalog);
                    settings.ConfigureActiveCatalog(activeCatalog);
                    I18nCatalogAutoCompiler.Queue(activeCatalog);
                    error = string.Empty;
                    return true;
                }

                List<I18nCatalogAsset> existingCatalogs = FindProjectCatalogs();
                if (existingCatalogs.Count > 1)
                {
                    error =
                        "Multiple GreenBox I18n catalog assets were found. " +
                        "Keep a single project catalog before continuing.";
                    return false;
                }

                I18nCatalogAsset catalogAsset;
                if (existingCatalogs.Count == 1)
                {
                    catalogAsset = EnsureRuntimeCatalogLocation(existingCatalogs[0]);
                    if (!catalogAsset.SourceCatalog)
                    {
                        TextAsset sourceCatalog = EnsureDefaultSourceFiles();
                        AssignSourceCatalog(catalogAsset, sourceCatalog);
                    }
                }
                else
                {
                    catalogAsset = CreateDefaultCatalogAsset();
                }

                settings.ConfigureActiveCatalog(catalogAsset);
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

        private static List<I18nCatalogAsset> FindProjectCatalogs()
        {
            string[] catalogGuids = AssetDatabase.FindAssets(
                "t:I18nCatalogAsset",
                new[] { "Assets" });
            var catalogs = new List<I18nCatalogAsset>(catalogGuids.Length);

            for (int catalogIndex = 0; catalogIndex < catalogGuids.Length; catalogIndex++)
            {
                string catalogPath = AssetDatabase.GUIDToAssetPath(catalogGuids[catalogIndex]);
                I18nCatalogAsset? catalog =
                    AssetDatabase.LoadAssetAtPath<I18nCatalogAsset>(catalogPath);
                if (catalog)
                {
                    catalogs.Add(catalog);
                }
            }

            return catalogs;
        }

        private static I18nCatalogAsset CreateDefaultCatalogAsset()
        {
            EnsureDefaultFolder();
            UnityEngine.Object? occupiedAsset =
                AssetDatabase.LoadMainAssetAtPath(I18nProjectLayout.CatalogAssetPath);
            if (occupiedAsset)
            {
                throw new InvalidOperationException(
                    $"Cannot create the project catalog because '{I18nProjectLayout.CatalogAssetPath}' " +
                    "is already occupied by another asset.");
            }

            TextAsset sourceCatalog = EnsureDefaultSourceFiles();
            var catalogAsset = ScriptableObject.CreateInstance<I18nCatalogAsset>();
            catalogAsset.name = "localization";
            AssetDatabase.CreateAsset(catalogAsset, I18nProjectLayout.CatalogAssetPath);
            AssignSourceCatalog(catalogAsset, sourceCatalog);
            return catalogAsset;
        }

        private static TextAsset EnsureDefaultSourceFiles()
        {
            EnsureDefaultFolder();
            WriteFileIfMissing(
                I18nProjectLayout.SourceCatalogPath,
                I18nProjectLayout.CreateInitialCatalogJson());
            WriteFileIfMissing(I18nProjectLayout.ReadmePath, I18nProjectLayout.CreateReadme());

            AssetDatabase.ImportAsset(
                I18nProjectLayout.SourceCatalogPath,
                ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(
                I18nProjectLayout.ReadmePath,
                ImportAssetOptions.ForceSynchronousImport);

            TextAsset? sourceCatalog =
                AssetDatabase.LoadAssetAtPath<TextAsset>(I18nProjectLayout.SourceCatalogPath);
            if (!sourceCatalog)
            {
                throw new InvalidOperationException(
                    $"Unity could not import '{I18nProjectLayout.SourceCatalogPath}' as a TextAsset.");
            }

            return sourceCatalog;
        }

        private static void EnsureDefaultFolder()
        {
            EnsureFolder("Assets", "GreenBox.I18n");
            EnsureFolder(I18nProjectLayout.RootFolderPath, "Resources");
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

        private static I18nCatalogAsset EnsureRuntimeCatalogLocation(
            I18nCatalogAsset catalogAsset)
        {
            string currentPath = AssetDatabase.GetAssetPath(catalogAsset);
            if (I18nProjectLayout.IsRuntimeCatalogPath(currentPath))
            {
                return catalogAsset;
            }

            EnsureDefaultFolder();
            UnityEngine.Object? occupiedAsset =
                AssetDatabase.LoadMainAssetAtPath(I18nProjectLayout.CatalogAssetPath);
            if (occupiedAsset && occupiedAsset != catalogAsset)
            {
                throw new InvalidOperationException(
                    $"Cannot move the project catalog to '{I18nProjectLayout.CatalogAssetPath}' " +
                    "because that path is occupied by another asset.");
            }

            string moveError = AssetDatabase.MoveAsset(
                currentPath,
                I18nProjectLayout.CatalogAssetPath);
            if (!string.IsNullOrEmpty(moveError))
            {
                throw new InvalidOperationException(
                    $"Could not move the project catalog to its runtime location. {moveError}");
            }

            I18nCatalogAsset? movedCatalog =
                AssetDatabase.LoadAssetAtPath<I18nCatalogAsset>(
                    I18nProjectLayout.CatalogAssetPath);
            return movedCatalog
                ? movedCatalog
                : throw new InvalidOperationException(
                    "Unity could not reload the project catalog after moving it.");
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

        private static void AssignSourceCatalog(
            I18nCatalogAsset catalogAsset,
            TextAsset sourceCatalog)
        {
            var serializedCatalog = new SerializedObject(catalogAsset);
            SerializedProperty? sourceProperty =
                serializedCatalog.FindProperty("_sourceCatalog");
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
