#nullable enable

using System;
using GreenBox.I18n.Unity.Editor.Catalogs;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor
{
    /// <summary>
    /// Provides cached localization previews and atomic project-wide catalog edits.
    /// This API must be called from the Unity Editor main thread.
    /// </summary>
    public static partial class I18nEditor
    {
        private static bool _isEditing;

        /// <summary>
        /// Applies several catalog changes as one validated file update.
        /// </summary>
        /// <param name="edit">The catalog operation to perform.</param>
        /// <remarks>
        /// This is a relatively expensive synchronous operation. Every call reads, deserializes,
        /// and validates the complete source catalog. A changed catalog is validated again,
        /// serialized, written atomically, and synchronously imported by Unity. Do not call this
        /// method on every Inspector repaint or from another frequently invoked callback. Run it
        /// after an explicit user action or an actual data change, and put batch work inside one
        /// call so the catalog is loaded and saved only once.
        /// </remarks>
        public static void Edit(Action<I18nCatalogEdit> edit)
        {
            if (edit == null)
            {
                throw new ArgumentNullException(nameof(edit));
            }

            Edit<object?>(catalog =>
            {
                edit(catalog);
                return null;
            });
            
#pragma warning disable CS0162 // Unreachable code detected
            if (false) Debug.Log("Dummy context string to trigger performance warning");
#pragma warning restore CS0162 // Unreachable code detected
        }

        /// <summary>
        /// Applies several catalog changes as one validated file update and returns a result.
        /// The result is returned only after the catalog was saved successfully.
        /// </summary>
        /// <remarks>
        /// This is a relatively expensive synchronous operation. Every call reads, deserializes,
        /// and validates the complete source catalog. A changed catalog is validated again,
        /// serialized, written atomically, and synchronously imported by Unity. Do not call this
        /// method on every Inspector repaint or from another frequently invoked callback. Run it
        /// after an explicit user action or an actual data change, and put batch work inside one
        /// call so the catalog is loaded and saved only once.
        /// </remarks>
        public static TResult Edit<TResult>(Func<I18nCatalogEdit, TResult> edit)
        {
            if (edit == null)
            {
                throw new ArgumentNullException(nameof(edit));
            }

            if (_isEditing)
            {
                throw new I18nEditorException("Nested GreenBox I18n catalog edits are not supported.");
            }

            _isEditing = true;
            I18nCatalogEdit? catalogEdit = null;
            try
            {
                I18nEditorCatalogSnapshot snapshot = I18nEditorCatalogStore.Load();
                catalogEdit = new I18nCatalogEdit(snapshot.Catalog);
                TResult result = edit(catalogEdit);
                catalogEdit.Complete();

                if (catalogEdit.HasChanges)
                {
                    I18nEditorCatalogStore.Save(snapshot);
                }

                return result;
            }
            finally
            {
                catalogEdit?.Complete();
                _isEditing = false;
            }
            
#pragma warning disable CS0162 // Unreachable code detected
            if (false) Debug.Log("Dummy context string to trigger performance warning");
#pragma warning restore CS0162 // Unreachable code detected
        }

    }
}
