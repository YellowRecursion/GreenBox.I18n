#nullable enable

using System.Collections.Generic;
using GreenBox.I18n.Unity.Editor.Catalogs;
using NUnit.Framework;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Tests
{
    public sealed class I18nEditorLocalizationTests
    {
        private const long EntryId = 3857333080842830204;

        [Test]
        public void Context_DefaultAndExplicitLocale_ResolveWithoutChangingEachOther()
        {
            I18nCatalogAsset asset = CreateCatalogAsset(new I18nEntry
            {
                Id = EntryId.ToString(),
                Path = "Tasks.Example",
                Locales = new Dictionary<string, I18nLocaleValue>
                {
                    ["en"] = new() { Text = "Task example" },
                    ["ru"] = new() { Text = "Пример задания" },
                },
            });

            try
            {
                var context = new I18nEditorLocalizationContext(asset);

                Assert.That(context.GetRuntime(null).Text(EntryId), Is.EqualTo("Task example"));
                Assert.That(context.GetRuntime("ru").Text(EntryId), Is.EqualTo("Пример задания"));
                Assert.That(context.GetRuntime(null).Text(EntryId), Is.EqualTo("Task example"));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void Context_ExplicitLocale_FormatsNamedArguments()
        {
            I18nCatalogAsset asset = CreateCatalogAsset(new I18nEntry
            {
                Id = EntryId.ToString(),
                Path = "Tasks.Progress",
                Locales = new Dictionary<string, I18nLocaleValue>
                {
                    ["en"] = new() { Text = "Progress: {$count}" },
                    ["ru"] = new() { Text = "Прогресс: {$count}" },
                },
            });

            try
            {
                var context = new I18nEditorLocalizationContext(asset);

                string text = context.GetRuntime("ru").Text(EntryId, ("count", 5));

                Assert.That(text, Is.EqualTo("Прогресс: 5"));
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void Context_DefaultAndExplicitLocale_ResolveLocalizedAssets()
        {
            var english = new Texture2D(1, 1) { name = "English" };
            var russian = new Texture2D(1, 1) { name = "Russian" };
            var bindings = new List<I18nAssetBinding>
            {
                new("11111111111111111111111111111111", string.Empty, english),
                new("22222222222222222222222222222222", string.Empty, russian),
            };
            I18nCatalogAsset asset = CreateCatalogAsset(
                new I18nEntry
                {
                    Id = EntryId.ToString(),
                    Path = "Tasks.Image",
                    Locales = new Dictionary<string, I18nLocaleValue>
                    {
                        ["en"] = new()
                        {
                            Asset = new I18nAssetReference
                            {
                                AssetGuid = "11111111111111111111111111111111",
                            },
                        },
                        ["ru"] = new()
                        {
                            Asset = new I18nAssetReference
                            {
                                AssetGuid = "22222222222222222222222222222222",
                            },
                        },
                    },
                },
                bindings);

            try
            {
                var context = new I18nEditorLocalizationContext(asset);
                I18nAssetReference defaultReference = context.GetRuntime(null).Asset(EntryId)!;
                I18nAssetReference russianReference = context.GetRuntime("ru").Asset(EntryId)!;

                Assert.That(context.ResolveAsset(defaultReference), Is.SameAs(english));
                Assert.That(context.ResolveAsset(russianReference), Is.SameAs(russian));
            }
            finally
            {
                Object.DestroyImmediate(asset);
                Object.DestroyImmediate(english);
                Object.DestroyImmediate(russian);
            }
        }

        private static I18nCatalogAsset CreateCatalogAsset(
            I18nEntry entry,
            List<I18nAssetBinding>? bindings = null)
        {
            var catalog = new I18nCatalog
            {
                DefaultLocale = "en",
                Locales = new List<I18nLocaleDefinition>
                {
                    new()
                    {
                        Id = "en",
                        DisplayName = "English",
                        Culture = "en-US",
                    },
                    new()
                    {
                        Id = "ru",
                        DisplayName = "Русский",
                        Culture = "ru-RU",
                    },
                },
                Entries = new List<I18nEntry> { entry },
            };
            I18nCompiledCatalogCompilation compilation =
                I18nCompiledCatalogCompiler.Compile(catalog);
            Assert.That(compilation.IsSuccess, Is.True);

            I18nCatalogAsset asset = ScriptableObject.CreateInstance<I18nCatalogAsset>();
            asset.ReplaceCompiledData(
                "test-source-hash",
                I18nCompiledCatalogBinary.Serialize(compilation.Catalog!),
                bindings ?? new List<I18nAssetBinding>());
            return asset;
        }
    }
}
