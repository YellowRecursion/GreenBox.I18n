using System.Collections.Generic;
using GreenBox.I18n;
using GreenBox.I18n.Unity;
using NUnit.Framework;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Tests
{
    public sealed class I18nLocaleSelectorTests
    {
        [Test]
        public void EditorPreview_AfterRuntimeLocaleChange_RestoresDefaultLocale()
        {
            I18nRuntime runtime = CreateRuntime(
                ("en", "en-US"),
                ("ru", "ru-RU"));
            runtime.SetLocale("ru");

            global::I18n.SelectDefaultLocaleForEditorPreview(runtime);

            Assert.That(runtime.CurrentLocale.Id, Is.EqualTo("en"));
        }

        [Test]
        public void SelectDeviceLocale_ExactCultureMatch_Wins()
        {
            I18nRuntime runtime = CreateRuntime(
                ("en", "en-US"),
                ("pt-BR", "pt-BR"),
                ("pt-PT", "pt-PT"));

            string selected = I18nLocaleSelector.SelectDeviceLocale(
                runtime.Locales,
                runtime.DefaultLocale.Id,
                "pt-PT",
                SystemLanguage.Portuguese);

            Assert.That(selected, Is.EqualTo("pt-PT"));
        }

        [Test]
        public void SelectDeviceLocale_LanguageOnly_PrefersMatchingLocaleId()
        {
            I18nRuntime runtime = CreateRuntime(
                ("en", "en-US"),
                ("ru-RU", "ru-RU"),
                ("ru", "ru-BY"));

            string selected = I18nLocaleSelector.SelectDeviceLocale(
                runtime.Locales,
                runtime.DefaultLocale.Id,
                string.Empty,
                SystemLanguage.Russian);

            Assert.That(selected, Is.EqualTo("ru"));
        }

        [Test]
        public void SelectDeviceLocale_SystemLanguage_FallsBackWhenCultureIsUnavailable()
        {
            I18nRuntime runtime = CreateRuntime(
                ("en", "en-US"),
                ("uk", "uk-UA"));

            string selected = I18nLocaleSelector.SelectDeviceLocale(
                runtime.Locales,
                runtime.DefaultLocale.Id,
                string.Empty,
                SystemLanguage.Ukrainian);

            Assert.That(selected, Is.EqualTo("uk"));
        }

        [Test]
        public void SelectDeviceLocale_ProcessCultureDisagrees_UsesUnitySystemLanguage()
        {
            I18nRuntime runtime = CreateRuntime(
                ("en", "en-US"),
                ("ru", "ru-RU"));

            string selected = I18nLocaleSelector.SelectDeviceLocale(
                runtime.Locales,
                runtime.DefaultLocale.Id,
                "en-US",
                SystemLanguage.Russian);

            Assert.That(selected, Is.EqualTo("ru"));
        }

        [Test]
        public void SelectDeviceLocale_UnsupportedLanguage_ReturnsDefault()
        {
            I18nRuntime runtime = CreateRuntime(
                ("en", "en-US"),
                ("ru", "ru-RU"));

            string selected = I18nLocaleSelector.SelectDeviceLocale(
                runtime.Locales,
                runtime.DefaultLocale.Id,
                "de-DE",
                SystemLanguage.German);

            Assert.That(selected, Is.EqualTo("en"));
        }

        [Test]
        public void SelectDeviceLocale_ChineseScript_MatchesLocaleId()
        {
            I18nRuntime runtime = CreateRuntime(
                ("en", "en-US"),
                ("zh-Hans", "zh-CN"),
                ("zh-Hant", "zh-TW"));

            string selected = I18nLocaleSelector.SelectDeviceLocale(
                runtime.Locales,
                runtime.DefaultLocale.Id,
                string.Empty,
                SystemLanguage.ChineseTraditional);

            Assert.That(selected, Is.EqualTo("zh-Hant"));
        }

        private static I18nRuntime CreateRuntime(
            params (string Id, string Culture)[] locales)
        {
            var catalog = new I18nCatalog
            {
                DefaultLocale = locales[0].Id,
                Locales = new List<I18nLocaleDefinition>(),
            };

            for (int localeIndex = 0; localeIndex < locales.Length; localeIndex++)
            {
                (string id, string culture) = locales[localeIndex];
                catalog.Locales.Add(new I18nLocaleDefinition
                {
                    Id = id,
                    DisplayName = id,
                    Culture = culture,
                });
            }

            return new I18nRuntime(catalog);
        }
    }
}
