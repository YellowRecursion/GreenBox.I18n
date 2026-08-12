#nullable enable

using System.Collections.Generic;
using System.IO;
using GreenBox.I18n.Unity.Editor.Catalogs;
using NUnit.Framework;

namespace GreenBox.I18n.Unity.Editor.Tests
{
    public sealed class I18nCatalogEditTests
    {
        [Test]
        public void EnsureEntry_AssignedExistingKey_MovesEntryAndPreservesId()
        {
            I18nCatalog catalog = CreateCatalog(CreateEntry("3857333080842830204", "Items.OldName"));
            var edit = new I18nCatalogEdit(catalog);

            I18nEditorEntry entry = edit.EnsureEntry(
                new I18nKey(3857333080842830204),
                "Items.NewName");

            Assert.That(entry.Key.Id, Is.EqualTo(3857333080842830204));
            Assert.That(entry.Path, Is.EqualTo("Items.NewName"));
            Assert.That(entry.WasCreated, Is.False);
            Assert.That(edit.HasChanges, Is.True);
        }

        [Test]
        public void EnsureEntry_MissingKeyAndExistingPath_AdoptsExistingEntry()
        {
            I18nCatalog catalog = CreateCatalog(CreateEntry("3857333080842830204", "Items.Hat"));
            var edit = new I18nCatalogEdit(catalog);

            I18nEditorEntry entry = edit.EnsureEntry(
                new I18nKey(3857333080842830453),
                "Items.Hat");

            Assert.That(entry.Key.Id, Is.EqualTo(3857333080842830204));
            Assert.That(entry.WasCreated, Is.False);
            Assert.That(edit.HasChanges, Is.False);
        }

        [Test]
        public void GetOrCreateEntry_NewPath_CreatesEntryAndSetsDefaultText()
        {
            I18nCatalog catalog = CreateCatalog();
            var edit = new I18nCatalogEdit(catalog);

            I18nEditorEntry entry = edit.GetOrCreateEntry("Items.Hat")
                .SetDefaultText("Hat")
                .SetComment("Cosmetic item name.");

            Assert.That(entry.Key.IsAssigned, Is.True);
            Assert.That(entry.WasCreated, Is.True);
            Assert.That(catalog.Entries, Has.Count.EqualTo(1));
            Assert.That(catalog.Entries[0].Locales["en"].Text, Is.EqualTo("Hat"));
            Assert.That(catalog.Entries[0].Comment, Is.EqualTo("Cosmetic item name."));
        }

        [Test]
        public void CompletedEdit_RejectsFurtherMutation()
        {
            I18nCatalog catalog = CreateCatalog(CreateEntry("3857333080842830204", "Items.Hat"));
            var edit = new I18nCatalogEdit(catalog);
            I18nEditorEntry entry = edit.FindByPath("Items.Hat")!;
            edit.Complete();

            Assert.Throws<I18nEditorException>(() => entry.SetDefaultText("Changed"));
        }

        [Test]
        public void StoreSave_ExternallyChangedSource_RejectsWithoutOverwrite()
        {
            string directory = Path.Combine(Path.GetTempPath(), "GreenBox.I18n.Tests", Path.GetRandomFileName());
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "localization.json");
            I18nCatalog catalog = CreateCatalog(CreateEntry("3857333080842830204", "Items.Hat"));
            string originalJson = I18nCatalogJson.Serialize(catalog);
            File.WriteAllText(path, originalJson);
            var snapshot = new I18nEditorCatalogSnapshot("Assets/localization.json", path, originalJson, catalog);
            catalog.SetEntryComment(3857333080842830204, "Generated entry.");
            File.WriteAllText(path, "external change");

            try
            {
                Assert.Throws<I18nEditorException>(() => I18nEditorCatalogStore.Save(snapshot));
                Assert.That(File.ReadAllText(path), Is.EqualTo("external change"));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        private static I18nCatalog CreateCatalog(params I18nEntry[] entries)
        {
            return new I18nCatalog
            {
                DefaultLocale = "en",
                Locales = new List<I18nLocaleDefinition>
                {
                    new I18nLocaleDefinition
                    {
                        Id = "en",
                        DisplayName = "English",
                        Culture = "en-US",
                    },
                    new I18nLocaleDefinition
                    {
                        Id = "ru",
                        DisplayName = "Русский",
                        Culture = "ru-RU",
                    },
                },
                Entries = new List<I18nEntry>(entries),
            };
        }

        private static I18nEntry CreateEntry(string id, string path)
        {
            return new I18nEntry
            {
                Id = id,
                Path = path,
                Locales = new Dictionary<string, I18nLocaleValue>(),
            };
        }
    }
}
