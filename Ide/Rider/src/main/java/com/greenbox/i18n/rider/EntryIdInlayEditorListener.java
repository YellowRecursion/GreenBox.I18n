package com.greenbox.i18n.rider;

import com.intellij.icons.AllIcons;
import com.intellij.openapi.application.ApplicationManager;
import com.intellij.openapi.editor.Document;
import com.intellij.openapi.editor.Editor;
import com.intellij.openapi.editor.EditorCustomElementRenderer;
import com.intellij.openapi.editor.Inlay;
import com.intellij.openapi.editor.colors.EditorFontType;
import com.intellij.openapi.editor.event.DocumentEvent;
import com.intellij.openapi.editor.event.DocumentListener;
import com.intellij.openapi.editor.event.EditorFactoryEvent;
import com.intellij.openapi.editor.event.EditorFactoryListener;
import com.intellij.openapi.fileEditor.FileDocumentManager;
import com.intellij.openapi.project.Project;
import com.intellij.openapi.util.Key;
import com.intellij.openapi.vfs.VirtualFile;
import com.intellij.ui.JBColor;
import org.jetbrains.annotations.NotNull;

import java.awt.Font;
import java.awt.FontMetrics;
import java.awt.Graphics;
import java.awt.Rectangle;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import javax.swing.Icon;

/**
 * Renders active-catalog entry names next to raw IDs without modifying C# source.
 * Semantic GreenBox call detection intentionally comes later.
 */
public final class EntryIdInlayEditorListener implements EditorFactoryListener {
    private static final Key<List<Inlay<?>>> INLAYS_KEY = Key.create("greenbox.i18n.entry.inlays");
    private static final Key<DocumentListener> DOCUMENT_LISTENER_KEY =
        Key.create("greenbox.i18n.entry.document.listener");
    private static final Key<Boolean> REFRESH_QUEUED_KEY =
        Key.create("greenbox.i18n.entry.refresh.queued");

    @Override
    public void editorCreated(@NotNull EditorFactoryEvent event) {
        attach(event.getEditor());
    }

    static boolean attach(Editor editor) {
        if (!isCSharpEditor(editor) || editor.getUserData(DOCUMENT_LISTENER_KEY) != null) {
            return false;
        }

        if (editor.isDisposed()) {
            return false;
        }

        DocumentListener listener = new DocumentListener() {
            @Override
            public void documentChanged(@NotNull DocumentEvent event) {
                queueRefresh(editor);
            }
        };

        editor.getDocument().addDocumentListener(listener);
        editor.putUserData(DOCUMENT_LISTENER_KEY, listener);
        queueRefresh(editor);
        return true;
    }

    static boolean containsKnownEntryId(Editor editor) {
        Project project = editor.getProject();
        if (editor.isDisposed() || project == null) {
            return false;
        }

        Map<String, String> entryNames = I18nProjectCatalogService.getInstance(project).entryNames();
        return findKnownEntryId(editor.getDocument().getText(), entryNames) != null;
    }

    static void refreshProjectEditors(Project project) {
        for (Editor editor : com.intellij.openapi.editor.EditorFactory.getInstance().getAllEditors()) {
            if (editor.getProject() == project) {
                queueRefresh(editor);
            }
        }
    }

    @Override
    public void editorReleased(@NotNull EditorFactoryEvent event) {
        Editor editor = event.getEditor();
        DocumentListener listener = editor.getUserData(DOCUMENT_LISTENER_KEY);
        if (listener != null) {
            editor.getDocument().removeDocumentListener(listener);
            editor.putUserData(DOCUMENT_LISTENER_KEY, null);
        }

        disposeInlays(editor);
    }

    private static boolean isCSharpEditor(Editor editor) {
        VirtualFile file = FileDocumentManager.getInstance().getFile(editor.getDocument());
        return file != null && "cs".equalsIgnoreCase(file.getExtension());
    }

    private static void queueRefresh(Editor editor) {
        if (Boolean.TRUE.equals(editor.getUserData(REFRESH_QUEUED_KEY))) {
            return;
        }

        editor.putUserData(REFRESH_QUEUED_KEY, true);
        ApplicationManager.getApplication().invokeLater(() -> {
            editor.putUserData(REFRESH_QUEUED_KEY, false);
            if (!editor.isDisposed()) {
                refresh(editor);
            }
        });
    }

    private static void refresh(Editor editor) {
        disposeInlays(editor);

        Document document = editor.getDocument();
        String text = document.getText();
        List<Inlay<?>> inlays = new ArrayList<>();
        Project project = editor.getProject();
        if (project == null) {
            editor.putUserData(INLAYS_KEY, inlays);
            return;
        }

        Map<String, String> entryNames = I18nProjectCatalogService.getInstance(project).entryNames();
        CSharpNumericTokenScanner.scan(text, (offset, id) -> {
            String entryName = entryNames.get(id);
            EditorCustomElementRenderer renderer = null;
            if (entryName != null) {
                renderer = new EntryNameRenderer(editor, entryName);
            } else if (I18nEntryIdFormat.isValid(id)) {
                renderer = new MissingEntryRenderer(editor);
            }

            if (renderer != null) {
                Inlay<?> inlay = editor.getInlayModel().addInlineElement(offset, false, renderer);
                if (inlay != null) {
                    inlays.add(inlay);
                }
            }
        });

        editor.putUserData(INLAYS_KEY, inlays);
    }

    private static String findKnownEntryId(String text, Map<String, String> entryNames) {
        String[] result = new String[1];
        CSharpNumericTokenScanner.scan(text, (offset, id) -> {
            if (entryNames.containsKey(id)) {
                result[0] = id;
            }
        });
        return result[0];
    }

    private static void disposeInlays(Editor editor) {
        List<Inlay<?>> inlays = editor.getUserData(INLAYS_KEY);
        if (inlays == null) {
            return;
        }

        for (Inlay<?> inlay : inlays) {
            if (inlay.isValid()) {
                inlay.dispose();
            }
        }
        editor.putUserData(INLAYS_KEY, null);
    }

    private static final class EntryNameRenderer implements EditorCustomElementRenderer {
        private final Editor editor;
        private final String text;

        private EntryNameRenderer(Editor editor, String text) {
            this.editor = editor;
            this.text = text;
        }

        @Override
        public int calcWidthInPixels(@NotNull Inlay inlay) {
            return fontMetrics().stringWidth(text);
        }

        @Override
        public void paint(
            @NotNull Inlay inlay,
            @NotNull Graphics graphics,
            @NotNull Rectangle targetRegion,
            @NotNull com.intellij.openapi.editor.markup.TextAttributes textAttributes) {
            graphics.setFont(editorFont());
            graphics.setColor(JBColor.GRAY);
            graphics.drawString(text, targetRegion.x, targetRegion.y + editor.getAscent());
        }

        private Font editorFont() {
            return editor.getColorsScheme().getFont(EditorFontType.PLAIN);
        }

        private FontMetrics fontMetrics() {
            return editor.getContentComponent().getFontMetrics(editorFont());
        }
    }

    private static final class MissingEntryRenderer implements EditorCustomElementRenderer {
        private static final Icon ICON = AllIcons.General.Warning;

        private final Editor editor;

        private MissingEntryRenderer(Editor editor) {
            this.editor = editor;
        }

        @Override
        public int calcWidthInPixels(@NotNull Inlay inlay) {
            return ICON.getIconWidth();
        }

        @Override
        public void paint(
            @NotNull Inlay inlay,
            @NotNull Graphics graphics,
            @NotNull Rectangle targetRegion,
            @NotNull com.intellij.openapi.editor.markup.TextAttributes textAttributes) {
            int iconY = targetRegion.y + (targetRegion.height - ICON.getIconHeight()) / 2;
            ICON.paintIcon(editor.getContentComponent(), graphics, targetRegion.x, iconY);
        }
    }
}
