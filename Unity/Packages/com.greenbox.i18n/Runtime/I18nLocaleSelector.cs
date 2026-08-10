#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using GreenBox.I18n;
using UnityEngine;

namespace GreenBox.I18n.Unity
{
    /// <summary>
    /// Selects the closest declared locale for the current device language.
    /// </summary>
    internal static class I18nLocaleSelector
    {
        internal static string SelectDeviceLocale(
            IReadOnlyList<I18nRuntimeLocale> locales,
            string defaultLocaleId)
        {
            return SelectDeviceLocale(
                locales,
                defaultLocaleId,
                CultureInfo.CurrentUICulture.Name,
                Application.systemLanguage);
        }

        internal static string SelectDeviceLocale(
            IReadOnlyList<I18nRuntimeLocale> locales,
            string defaultLocaleId,
            string? currentUiCultureName,
            SystemLanguage systemLanguage)
        {
            string? systemLanguageTag = GetLanguageTag(systemLanguage);
            bool cultureMatchesSystemLanguage =
                systemLanguageTag == null ||
                string.Equals(
                    GetLanguageSubtag(currentUiCultureName),
                    GetLanguageSubtag(systemLanguageTag),
                    StringComparison.OrdinalIgnoreCase);

            string? exactMatch = cultureMatchesSystemLanguage
                ? FindExact(locales, currentUiCultureName)
                : null;
            exactMatch ??= FindExact(locales, systemLanguageTag);
            if (exactMatch != null)
            {
                return exactMatch;
            }

            string? languageMatch = FindLanguage(locales, systemLanguageTag);
            if (languageMatch == null && cultureMatchesSystemLanguage)
            {
                languageMatch = FindLanguage(locales, currentUiCultureName);
            }

            return languageMatch ?? defaultLocaleId;
        }

        private static string? FindExact(
            IReadOnlyList<I18nRuntimeLocale> locales,
            string? languageTag)
        {
            if (string.IsNullOrWhiteSpace(languageTag))
            {
                return null;
            }

            for (int localeIndex = 0; localeIndex < locales.Count; localeIndex++)
            {
                I18nRuntimeLocale locale = locales[localeIndex];
                if (string.Equals(locale.Id, languageTag, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        locale.Culture.Name,
                        languageTag,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return locale.Id;
                }
            }

            return null;
        }

        private static string? FindLanguage(
            IReadOnlyList<I18nRuntimeLocale> locales,
            string? languageTag)
        {
            string? language = GetLanguageSubtag(languageTag);
            if (language == null)
            {
                return null;
            }

            for (int localeIndex = 0; localeIndex < locales.Count; localeIndex++)
            {
                I18nRuntimeLocale locale = locales[localeIndex];
                if (string.Equals(locale.Id, language, StringComparison.OrdinalIgnoreCase))
                {
                    return locale.Id;
                }
            }

            for (int localeIndex = 0; localeIndex < locales.Count; localeIndex++)
            {
                I18nRuntimeLocale locale = locales[localeIndex];
                if (string.Equals(
                        locale.Culture.TwoLetterISOLanguageName,
                        language,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return locale.Id;
                }
            }

            return null;
        }

        private static string? GetLanguageSubtag(string? languageTag)
        {
            if (string.IsNullOrWhiteSpace(languageTag))
            {
                return null;
            }

            int separatorIndex = languageTag.IndexOf('-');
            return separatorIndex < 0
                ? languageTag
                : languageTag.Substring(0, separatorIndex);
        }

        private static string? GetLanguageTag(SystemLanguage language)
        {
            return language switch
            {
                SystemLanguage.Afrikaans => "af",
                SystemLanguage.Arabic => "ar",
                SystemLanguage.Basque => "eu",
                SystemLanguage.Belarusian => "be",
                SystemLanguage.Bulgarian => "bg",
                SystemLanguage.Catalan => "ca",
                SystemLanguage.Chinese => "zh",
                SystemLanguage.ChineseSimplified => "zh-Hans",
                SystemLanguage.ChineseTraditional => "zh-Hant",
                SystemLanguage.Czech => "cs",
                SystemLanguage.Danish => "da",
                SystemLanguage.Dutch => "nl",
                SystemLanguage.English => "en",
                SystemLanguage.Estonian => "et",
                SystemLanguage.Faroese => "fo",
                SystemLanguage.Finnish => "fi",
                SystemLanguage.French => "fr",
                SystemLanguage.German => "de",
                SystemLanguage.Greek => "el",
                SystemLanguage.Hebrew => "he",
                SystemLanguage.Hindi => "hi",
                SystemLanguage.Hungarian => "hu",
                SystemLanguage.Icelandic => "is",
                SystemLanguage.Indonesian => "id",
                SystemLanguage.Italian => "it",
                SystemLanguage.Japanese => "ja",
                SystemLanguage.Korean => "ko",
                SystemLanguage.Latvian => "lv",
                SystemLanguage.Lithuanian => "lt",
                SystemLanguage.Norwegian => "no",
                SystemLanguage.Polish => "pl",
                SystemLanguage.Portuguese => "pt",
                SystemLanguage.Romanian => "ro",
                SystemLanguage.Russian => "ru",
                SystemLanguage.SerboCroatian => "sr",
                SystemLanguage.Slovak => "sk",
                SystemLanguage.Slovenian => "sl",
                SystemLanguage.Spanish => "es",
                SystemLanguage.Swedish => "sv",
                SystemLanguage.Thai => "th",
                SystemLanguage.Turkish => "tr",
                SystemLanguage.Ukrainian => "uk",
                SystemLanguage.Vietnamese => "vi",
                _ => null,
            };
        }
    }
}
