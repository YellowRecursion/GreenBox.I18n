#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using GreenBox.I18n.Unity.Editor.Settings;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Catalogs
{
    internal sealed class I18nEditorCatalogSnapshot
    {
        internal I18nEditorCatalogSnapshot(
            string assetPath,
            string absolutePath,
            string originalJson,
            I18nCatalog catalog)
        {
            AssetPath = assetPath;
            AbsolutePath = absolutePath;
            OriginalJson = originalJson;
            Catalog = catalog;
        }

        internal string AssetPath { get; }

        internal string AbsolutePath { get; }

        internal string OriginalJson { get; }

        internal I18nCatalog Catalog { get; }
    }

    /// <summary>
    /// Owns Unity discovery and crash-safe persistence for public editor transactions.
    /// </summary>
    internal static class I18nEditorCatalogStore
    {
        internal static I18nEditorCatalogSnapshot Load()
        {
            TextAsset? source = I18nProjectSettings.instance.SourceCatalog;
            if (!source)
            {
                throw new I18nEditorException(
                    "The active GreenBox I18n source catalog is unavailable. Let project setup repair it first.");
            }

            string assetPath = AssetDatabase.GetAssetPath(source);
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new I18nEditorException("The Unity project root could not be resolved.");
            string absolutePath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));

            try
            {
                string json = File.ReadAllText(absolutePath, Encoding.UTF8);
                I18nCatalog catalog = I18nCatalogJson.Deserialize(json);
                ThrowIfInvalid(catalog, "The active localization catalog is invalid and cannot be edited");
                return new I18nEditorCatalogSnapshot(assetPath, absolutePath, json, catalog);
            }
            catch (I18nEditorException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is JsonException)
            {
                throw new I18nEditorException(
                    $"The active localization catalog could not be loaded: {exception.Message}",
                    exception);
            }
        }

        internal static void Save(I18nEditorCatalogSnapshot snapshot)
        {
            ThrowIfInvalid(snapshot.Catalog, "The requested localization changes are invalid");
            string json = I18nCatalogJson.Serialize(snapshot.Catalog);
            if (string.Equals(json, snapshot.OriginalJson, StringComparison.Ordinal))
            {
                return;
            }

            string currentJson;
            try
            {
                currentJson = File.ReadAllText(snapshot.AbsolutePath, Encoding.UTF8);
            }
            catch (Exception exception) when (
                exception is IOException || exception is UnauthorizedAccessException)
            {
                throw new I18nEditorException(
                    $"The localization catalog could not be checked before saving: {exception.Message}",
                    exception);
            }

            if (!string.Equals(currentJson, snapshot.OriginalJson, StringComparison.Ordinal))
            {
                throw new I18nEditorException(
                    "The localization catalog changed outside this Unity edit. " +
                    "No data was overwritten; run the operation again against the latest catalog.");
            }

            WriteAtomically(snapshot.AbsolutePath, json);
            AssetDatabase.ImportAsset(
                snapshot.AssetPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        private static void ThrowIfInvalid(I18nCatalog catalog, string message)
        {
            I18nValidationResult validation = I18nCatalogValidator.Validate(catalog);
            if (!validation.HasErrors)
            {
                return;
            }

            string details = string.Join(
                Environment.NewLine,
                validation.Diagnostics
                    .Where(diagnostic => diagnostic.Severity == I18nValidationSeverity.Error)
                    .Take(5)
                    .Select(diagnostic => diagnostic.ToString()));
            throw new I18nEditorException($"{message}:{Environment.NewLine}{details}");
        }

        private static void WriteAtomically(string path, string content)
        {
            string directory = Path.GetDirectoryName(path)!;
            string temporaryPath = Path.Combine(
                directory,
                $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

            try
            {
                using (var stream = new FileStream(
                           temporaryPath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(content);
                    writer.Flush();
                    stream.Flush(true);
                }

                File.Replace(temporaryPath, path, null);
            }
            catch (Exception exception) when (
                exception is IOException || exception is UnauthorizedAccessException)
            {
                throw new I18nEditorException(
                    $"The localization catalog could not be saved: {exception.Message}",
                    exception);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
                catch (Exception exception) when (
                    exception is IOException || exception is UnauthorizedAccessException)
                {
                    // The source catalog is intact; a stale temporary file can be removed later.
                }
            }
        }
    }
}
