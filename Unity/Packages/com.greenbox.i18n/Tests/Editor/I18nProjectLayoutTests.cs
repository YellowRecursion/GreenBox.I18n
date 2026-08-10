using GreenBox.I18n;
using GreenBox.I18n.Unity.Editor.Setup;
using NUnit.Framework;

namespace GreenBox.I18n.Unity.Editor.Tests
{
    public sealed class I18nProjectLayoutTests
    {
        [Test]
        public void InitialCatalog_IsValidAndContainsEditingNotice()
        {
            string json = I18nProjectLayout.CreateInitialCatalogJson();

            I18nCatalog catalog = I18nCatalogJson.Deserialize(json);
            I18nValidationResult validation = I18nCatalogValidator.Validate(catalog);

            Assert.That(validation.HasErrors, Is.False);
            Assert.That(catalog.FileComment, Is.EqualTo(I18nCatalog.ManagedFileComment));
            Assert.That(catalog.DefaultLocale, Is.EqualTo("en"));
            Assert.That(catalog.Locales, Has.Count.EqualTo(1));
            Assert.That(catalog.Entries, Is.Empty);
        }

        [Test]
        public void DefaultFileNames_AreLowercaseAndDocumented()
        {
            Assert.That(I18nProjectLayout.SourceCatalogPath, Does.EndWith("/localization.json"));
            Assert.That(I18nProjectLayout.CatalogAssetPath, Does.EndWith("/greenbox-i18n.asset"));
            Assert.That(
                I18nProjectLayout.CatalogAssetPath,
                Does.EndWith(I18nProjectLayout.RuntimeCatalogPathSuffix));
            Assert.That(
                I18nProjectLayout.IsRuntimeCatalogPath(
                    "Assets/Custom/Resources/greenbox-i18n.asset"),
                Is.True);
            Assert.That(
                I18nProjectLayout.IsRuntimeCatalogPath(
                    "Assets/Custom/localization.asset"),
                Is.False);
            Assert.That(I18nProjectLayout.ReadmePath, Does.EndWith("/readme.md"));

            string readme = I18nProjectLayout.CreateReadme();
            Assert.That(readme, Does.Contain("localization.json"));
            Assert.That(readme, Does.Contain("greenbox-i18n.asset"));
            Assert.That(readme, Does.Contain("move this whole folder"));
            Assert.That(readme, Does.Contain("Do not rename, move, replace, or delete"));
            Assert.That(readme, Does.Contain("validates and repairs"));
        }
    }
}
