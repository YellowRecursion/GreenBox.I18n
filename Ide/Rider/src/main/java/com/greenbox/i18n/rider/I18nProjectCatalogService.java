package com.greenbox.i18n.rider;

import com.intellij.openapi.Disposable;
import com.intellij.openapi.diagnostic.Logger;
import com.intellij.openapi.project.Project;
import com.intellij.openapi.vfs.VirtualFileManager;
import com.intellij.openapi.vfs.newvfs.BulkFileListener;
import com.intellij.openapi.vfs.newvfs.events.VFileEvent;
import org.jetbrains.annotations.NotNull;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.InvalidPathException;
import java.nio.file.Path;
import java.util.List;
import java.util.Map;

/** Maintains a project-local, immutable entry index for editor features. */
public final class I18nProjectCatalogService implements Disposable {
    private static final Logger LOG = Logger.getInstance(I18nProjectCatalogService.class);

    private final Project project;
    private volatile Snapshot snapshot = Snapshot.empty();

    public I18nProjectCatalogService(Project project) {
        this.project = project;
        project.getMessageBus().connect(this).subscribe(
            VirtualFileManager.VFS_CHANGES,
            new BulkFileListener() {
                @Override
                public void after(@NotNull List<? extends VFileEvent> events) {
                    if (events.stream().anyMatch(I18nProjectCatalogService.this::isRelevant)) {
                        reload();
                    }
                }
            });
    }

    static I18nProjectCatalogService getInstance(Project project) {
        return project.getService(I18nProjectCatalogService.class);
    }

    Map<String, I18nCatalogEntry> entries() {
        return snapshot.entries();
    }

    synchronized void reload() {
        try {
            I18nProjectCatalogLocator.CatalogLocation location = I18nProjectCatalogLocator.locate(project);
            if (location == null) {
                updateSnapshot(Snapshot.empty());
                return;
            }

            Path catalogPath = location.catalogPath();
            if (catalogPath == null || !Files.isRegularFile(catalogPath)) {
                updateSnapshot(new Snapshot(location.settingsPath(), catalogPath, Map.of()));
                return;
            }

            Map<String, I18nCatalogEntry> entries = I18nCatalogReader.readEntries(catalogPath);
            updateSnapshot(new Snapshot(location.settingsPath(), catalogPath, entries));
            LOG.info("Loaded " + entries.size() + " GreenBox I18n entries from " + catalogPath + '.');
        } catch (IOException | RuntimeException exception) {
            LOG.warn("Could not load the active GreenBox I18n catalog. Keeping the previous index.", exception);
        }
    }

    private void updateSnapshot(Snapshot newSnapshot) {
        Snapshot previous = snapshot;
        snapshot = newSnapshot;
        if (!previous.equals(newSnapshot)) {
            EntryIdInlayEditorListener.refreshProjectEditors(project);
        }
    }

    private boolean isRelevant(VFileEvent event) {
        Snapshot current = snapshot;
        try {
            Path eventPath = Path.of(event.getPath()).toAbsolutePath().normalize();
            return eventPath.equals(current.settingsPath()) || eventPath.equals(current.catalogPath());
        } catch (InvalidPathException exception) {
            return false;
        }
    }

    @Override
    public void dispose() {
    }

    private record Snapshot(Path settingsPath, Path catalogPath, Map<String, I18nCatalogEntry> entries) {
        private static Snapshot empty() {
            return new Snapshot(null, null, Map.of());
        }
    }
}
