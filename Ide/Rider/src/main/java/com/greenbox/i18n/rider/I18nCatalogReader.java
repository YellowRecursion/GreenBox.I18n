package com.greenbox.i18n.rider;

import com.google.gson.stream.JsonReader;
import com.google.gson.stream.JsonToken;

import java.io.BufferedReader;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.HashMap;
import java.util.Map;

/** Reads only entry IDs and paths while skipping potentially large localization values. */
final class I18nCatalogReader {
    private I18nCatalogReader() {
    }

    static Map<String, String> readEntryNames(Path catalogPath) throws IOException {
        Map<String, String> entries = new HashMap<>();
        try (BufferedReader textReader = Files.newBufferedReader(catalogPath, StandardCharsets.UTF_8);
             JsonReader reader = new JsonReader(textReader)) {
            reader.beginObject();
            while (reader.hasNext()) {
                String propertyName = reader.nextName();
                if ("entries".equals(propertyName)) {
                    readEntries(reader, entries);
                } else {
                    reader.skipValue();
                }
            }
            reader.endObject();
        }
        return Map.copyOf(entries);
    }

    private static void readEntries(JsonReader reader, Map<String, String> entries) throws IOException {
        if (reader.peek() != JsonToken.BEGIN_ARRAY) {
            reader.skipValue();
            return;
        }

        reader.beginArray();
        while (reader.hasNext()) {
            readEntry(reader, entries);
        }
        reader.endArray();
    }

    private static void readEntry(JsonReader reader, Map<String, String> entries) throws IOException {
        if (reader.peek() != JsonToken.BEGIN_OBJECT) {
            reader.skipValue();
            return;
        }

        String id = null;
        String path = null;
        reader.beginObject();
        while (reader.hasNext()) {
            String propertyName = reader.nextName();
            if ("id".equals(propertyName) && reader.peek() != JsonToken.NULL) {
                id = reader.nextString();
            } else if ("path".equals(propertyName) && reader.peek() != JsonToken.NULL) {
                path = reader.nextString();
            } else {
                reader.skipValue();
            }
        }
        reader.endObject();

        if (id != null && path != null && !id.isBlank() && !path.isBlank()) {
            entries.put(id, entryName(path));
        }
    }

    private static String entryName(String path) {
        int separator = path.lastIndexOf('.');
        return separator >= 0 ? path.substring(separator + 1) : path;
    }
}
