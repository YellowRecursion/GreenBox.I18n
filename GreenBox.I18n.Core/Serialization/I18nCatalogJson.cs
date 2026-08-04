using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GreenBox.I18n
{
    /// <summary>
    /// Serializes and deserializes the canonical JSON representation of an i18n catalog.
    /// </summary>
    public static class I18nCatalogJson
    {
        /// <summary>
        /// Deserializes an i18n catalog from JSON without validating its contents.
        /// </summary>
        /// <param name="json">The JSON representation of the catalog.</param>
        /// <returns>The deserialized catalog.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="json"/> is null.</exception>
        /// <exception cref="JsonException">Thrown when the JSON cannot be deserialized as a catalog.</exception>
        public static I18nCatalog Deserialize(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            using var stringReader = new StringReader(json);
            using var jsonReader = new JsonTextReader(stringReader);
            jsonReader.Culture = CultureInfo.InvariantCulture;
            jsonReader.DateParseHandling = DateParseHandling.None;
            jsonReader.MaxDepth = 64;

            JToken token = JToken.Load(
                jsonReader,
                new JsonLoadSettings
                {
                    CommentHandling = CommentHandling.Ignore,
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                    LineInfoHandling = LineInfoHandling.Load,
                });

            if (jsonReader.Read())
            {
                throw new JsonSerializationException("Additional content was found after the catalog JSON.");
            }

            I18nCatalog? catalog = token.ToObject<I18nCatalog>(CreateSerializer());
            if (catalog == null)
            {
                throw new JsonSerializationException("The catalog JSON cannot be null.");
            }

            return catalog;
        }

        /// <summary>
        /// Serializes an i18n catalog to canonical formatted JSON without validating it.
        /// </summary>
        /// <param name="catalog">The catalog to serialize.</param>
        /// <returns>The canonical JSON followed by a single line-feed character.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog"/> is null.</exception>
        public static string Serialize(I18nCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            string json = JsonConvert.SerializeObject(catalog, CreateSettings());
            return NormalizeLineEndings(json).TrimEnd('\n') + "\n";
        }

        private static JsonSerializer CreateSerializer()
        {
            return JsonSerializer.Create(CreateSettings());
        }

        private static JsonSerializerSettings CreateSettings()
        {
            var settings = new JsonSerializerSettings
            {
                Culture = CultureInfo.InvariantCulture,
                Formatting = Formatting.Indented,
                MaxDepth = 64,
                MissingMemberHandling = MissingMemberHandling.Error,
                NullValueHandling = NullValueHandling.Ignore,
            };

            settings.Converters.Add(new EntryListJsonConverter());
            settings.Converters.Add(new LocaleDictionaryJsonConverter());
            return settings;
        }

        private static string NormalizeLineEndings(string value)
        {
            return value.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        private sealed class EntryListJsonConverter : JsonConverter
        {
            public override bool CanRead => false;

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(List<I18nEntry>);
            }

            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            {
                var entries = (List<I18nEntry>)value!;

                writer.WriteStartArray();

                foreach (I18nEntry entry in entries.OrderBy(entry => entry, I18nEntryComparer.Canonical))
                {
                    serializer.Serialize(writer, entry);
                }

                writer.WriteEndArray();
            }

            public override object ReadJson(
                JsonReader reader,
                Type objectType,
                object? existingValue,
                JsonSerializer serializer)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class LocaleDictionaryJsonConverter : JsonConverter
        {
            public override bool CanRead => false;

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Dictionary<string, I18nLocaleValue>);
            }

            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            {
                var locales = (Dictionary<string, I18nLocaleValue>)value!;

                writer.WriteStartObject();

                foreach (string localeId in locales.Keys.OrderBy(id => id, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(localeId);
                    serializer.Serialize(writer, locales[localeId]);
                }

                writer.WriteEndObject();
            }

            public override object ReadJson(
                JsonReader reader,
                Type objectType,
                object? existingValue,
                JsonSerializer serializer)
            {
                throw new NotSupportedException();
            }
        }
    }
}
