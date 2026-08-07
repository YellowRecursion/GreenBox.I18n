package com.greenbox.i18n.rider;

import com.intellij.openapi.diagnostic.Logger;
import com.intellij.openapi.editor.Editor;
import com.intellij.openapi.editor.EditorFactory;
import com.intellij.openapi.project.DumbAware;
import com.intellij.openapi.project.Project;
import com.intellij.openapi.startup.StartupActivity;
import org.jetbrains.annotations.NotNull;

/** Attaches GreenBox inlays to editors restored before plugin listeners become active. */
public final class EntryIdInlayStartupActivity implements StartupActivity, DumbAware {
    private static final Logger LOG = Logger.getInstance(EntryIdInlayStartupActivity.class);

    @Override
    public void runActivity(@NotNull Project project) {
        I18nProjectCatalogService.getInstance(project).reload();

        int attachedEditors = 0;
        int matchingEditors = 0;

        for (Editor editor : EditorFactory.getInstance().getAllEditors()) {
            if (editor.getProject() != project) {
                continue;
            }

            if (EntryIdInlayEditorListener.containsKnownEntryId(editor)) {
                matchingEditors++;
            }
            if (EntryIdInlayEditorListener.attach(editor)) {
                attachedEditors++;
            }
        }

        LOG.info("GreenBox I18n inlays attached to " + attachedEditors
            + " restored editor(s); " + matchingEditors + " contain known entry IDs.");
    }
}
