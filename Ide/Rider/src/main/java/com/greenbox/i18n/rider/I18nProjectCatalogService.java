package com.greenbox.i18n.rider;

import com.intellij.openapi.Disposable;
import com.intellij.openapi.application.ApplicationManager;
import com.intellij.openapi.diagnostic.Logger;
import com.intellij.openapi.project.Project;
import com.intellij.util.Alarm;
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
    private static final int SOURCE_POLL_INTERVAL_MS = 1_000;

    private final Project project;
    private final Alarm sourcePollAlarm;
    private volatile Snapshot snapshot = Snapshot.empty();
    private volatile SourceFingerprint observedSource = SourceFingerprint.empty();
    private volatile boolean disposed;

    public I18nProjectCatalogService(Project project) {
        this.project = project;
        sourcePollAlarm = new Alarm(Alarm.ThreadToUse.POOLED_THREAD, this);
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
        scheduleSourcePoll();
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
            SourceFingerprint sourceBeforeRead = SourceFingerprint.capture(project, location);
            if (location == null) {
                observedSource = sourceBeforeRead;
                updateSnapshot(Snapshot.empty());
                return;
            }

            Path catalogPath = location.catalogPath();
            if (catalogPath == null || !Files.isRegularFile(catalogPath)) {
                observedSource = sourceBeforeRead;
                updateSnapshot(new Snapshot(location.settingsPath(), catalogPath, Map.of()));
                return;
            }

            Map<String, I18nCatalogEntry> entries = I18nCatalogReader.readEntries(catalogPath);
            I18nProjectCatalogLocator.CatalogLocation locationAfterRead =
                I18nProjectCatalogLocator.locate(project);
            SourceFingerprint sourceAfterRead = SourceFingerprint.capture(project, locationAfterRead);
            if (!sourceBeforeRead.equals(sourceAfterRead)) {
                return;
            }

            observedSource = sourceAfterRead;
            updateSnapshot(new Snapshot(location.settingsPath(), catalogPath, entries));
            LOG.info("Loaded " + entries.size() + " GreenBox I18n entries from " + catalogPath + '.');
        } catch (IOException | RuntimeException exception) {
            observedSource = captureSourceSafely();
            LOG.warn("Could not load the active GreenBox I18n catalog. Keeping the previous index.", exception);
        }
    }

    private void updateSnapshot(Snapshot newSnapshot) {
        Snapshot previous = snapshot;
        snapshot = newSnapshot;
        if (!previous.equals(newSnapshot)) {
            ApplicationManager.getApplication().invokeLater(() -> {
                if (!project.isDisposed()) {
                    EntryIdInlayEditorListener.refreshProjectEditors(project);
                }
            });
        }
    }

    private void scheduleSourcePoll() {
        if (disposed || project.isDisposed()) {
            return;
        }

        sourcePollAlarm.addRequest(this::pollSource, SOURCE_POLL_INTERVAL_MS);
    }

    private void pollSource() {
        try {
            SourceFingerprint currentSource = captureSourceSafely();
            if (!currentSource.equals(observedSource)) {
                reload();
            }
        } finally {
            scheduleSourcePoll();
        }
    }

    private SourceFingerprint captureSourceSafely() {
        try {
            I18nProjectCatalogLocator.CatalogLocation location = I18nProjectCatalogLocator.locate(project);
            return SourceFingerprint.capture(project, location);
        } catch (IOException | RuntimeException exception) {
            return SourceFingerprint.captureSettingsOnly(project);
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
        disposed = true;
    }

    private record Snapshot(Path settingsPath, Path catalogPath, Map<String, I18nCatalogEntry> entries) {
        private static Snapshot empty() {
            return new Snapshot(null, null, Map.of());
        }
    }

    private record SourceFingerprint(FileFingerprint settings, FileFingerprint catalog) {
        private static SourceFingerprint empty() {
            return new SourceFingerprint(FileFingerprint.missing(null), FileFingerprint.missing(null));
        }

        private static SourceFingerprint capture(
            Project project,
            I18nProjectCatalogLocator.CatalogLocation location) {
            Path settingsPath = location == null
                ? I18nProjectCatalogLocator.projectSettingsPath(project)
                : location.settingsPath();
            Path catalogPath = location == null ? null : location.catalogPath();
            return new SourceFingerprint(
                FileFingerprint.capture(settingsPath),
                FileFingerprint.capture(catalogPath));
        }

        private static SourceFingerprint captureSettingsOnly(Project project) {
            return new SourceFingerprint(
                FileFingerprint.capture(I18nProjectCatalogLocator.projectSettingsPath(project)),
                FileFingerprint.missing(null));
        }
    }

    private record FileFingerprint(Path path, boolean exists, long modifiedAtMillis, long size) {
        private static FileFingerprint capture(Path path) {
            if (path == null || !Files.isRegularFile(path)) {
                return missing(path);
            }

            try {
                return new FileFingerprint(
                    path,
                    true,
                    Files.getLastModifiedTime(path).toMillis(),
                    Files.size(path));
            } catch (IOException exception) {
                return missing(path);
            }
        }

        private static FileFingerprint missing(Path path) {
            return new FileFingerprint(path, false, 0, 0);
        }
    }
}
