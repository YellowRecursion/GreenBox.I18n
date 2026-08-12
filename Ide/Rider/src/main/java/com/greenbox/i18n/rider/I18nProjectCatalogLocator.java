package com.greenbox.i18n.rider;

import com.intellij.openapi.project.Project;
import org.jetbrains.annotations.NotNull;
import org.jetbrains.annotations.Nullable;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.InvalidPathException;
import java.nio.file.Path;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/** Resolves the GreenBox source catalog from Unity Project Settings. */
final class I18nProjectCatalogLocator {
    private static final String PROJECT_SETTINGS_PATH = "ProjectSettings/GreenBox.I18n.asset";
    private static final Pattern SOURCE_CATALOG_PATH = Pattern.compile(
        "(?m)^\\s*_sourceCatalogPath:\\s*(.*?)\\s*$");

    private I18nProjectCatalogLocator() {
    }

    static @Nullable Path projectSettingsPath(@NotNull Project project) {
        String basePath = project.getBasePath();
        if (basePath == null || basePath.isBlank()) {
            return null;
        }

        return Path.of(basePath)
            .toAbsolutePath()
            .normalize()
            .resolve(PROJECT_SETTINGS_PATH)
            .normalize();
    }

    static @Nullable CatalogLocation locate(@NotNull Project project) throws IOException {
        Path settingsPath = projectSettingsPath(project);
        if (settingsPath == null) {
            return null;
        }

        Path projectRoot = settingsPath.getParent().getParent();
        if (!Files.isRegularFile(settingsPath)) {
            return null;
        }

        String settings = Files.readString(settingsPath, StandardCharsets.UTF_8);
        Matcher matcher = SOURCE_CATALOG_PATH.matcher(settings);
        if (!matcher.find()) {
            return new CatalogLocation(projectRoot, settingsPath, null);
        }

        String relativeCatalogPath = decodeYamlScalar(matcher.group(1));
        if (relativeCatalogPath.isBlank()) {
            return new CatalogLocation(projectRoot, settingsPath, null);
        }

        Path catalogPath;
        try {
            catalogPath = projectRoot
                .resolve(relativeCatalogPath.replace('/', java.io.File.separatorChar))
                .normalize();
        } catch (InvalidPathException exception) {
            return new CatalogLocation(projectRoot, settingsPath, null);
        }
        if (!catalogPath.startsWith(projectRoot)) {
            return new CatalogLocation(projectRoot, settingsPath, null);
        }

        return new CatalogLocation(projectRoot, settingsPath, catalogPath);
    }

    private static String decodeYamlScalar(String value) {
        String trimmed = value.trim();
        if (trimmed.length() >= 2 && trimmed.startsWith("'") && trimmed.endsWith("'")) {
            return trimmed.substring(1, trimmed.length() - 1).replace("''", "'");
        }
        if (trimmed.length() >= 2 && trimmed.startsWith("\"") && trimmed.endsWith("\"")) {
            return trimmed.substring(1, trimmed.length() - 1)
                .replace("\\\"", "\"")
                .replace("\\\\", "\\");
        }
        return trimmed;
    }

    record CatalogLocation(Path projectRoot, Path settingsPath, @Nullable Path catalogPath) {
    }
}
