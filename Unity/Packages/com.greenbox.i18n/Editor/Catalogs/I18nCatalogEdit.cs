#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;

namespace GreenBox.I18n.Unity.Editor
{
    /// <summary>
    /// Represents one in-memory catalog transaction owned by <see cref="I18nEditor"/>.
    /// </summary>
    public sealed class I18nCatalogEdit
    {
        private readonly I18nCatalog _catalog;
        private readonly IReadOnlyList<string> _localeIds;
        private bool _isComplete;

        internal I18nCatalogEdit(I18nCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            var localeIds = new string[catalog.Locales.Count];
            for (int localeIndex = 0; localeIndex < catalog.Locales.Count; localeIndex++)
            {
                localeIds[localeIndex] = catalog.Locales[localeIndex].Id;
            }

            _localeIds = Array.AsReadOnly(localeIds);
        }

        /// <summary>Gets the catalog's final fallback locale.</summary>
        public string DefaultLocale => _catalog.DefaultLocale;

        /// <summary>Gets the declared locale identifiers in editor display order.</summary>
        public IReadOnlyList<string> LocaleIds => _localeIds;

        internal bool HasChanges { get; private set; }

        /// <summary>Creates an empty entry and allocates its stable key.</summary>
        public I18nEditorEntry CreateEntry(string path)
        {
            EnsureActive();
            I18nEditResult result = _catalog.AddEntry(path);
            I18nEntry entry = RequireSuccess(result);
            HasChanges |= result.HasChanges;
            return new I18nEditorEntry(this, ParseId(entry), true);
        }

        /// <summary>Returns the entry at a path, creating it when necessary.</summary>
        public I18nEditorEntry GetOrCreateEntry(string path)
        {
            EnsureActive();
            I18nEntry? existing = _catalog.FindByPath(path);
            return existing == null
                ? CreateEntry(path)
                : new I18nEditorEntry(this, ParseId(existing), false);
        }

        /// <summary>
        /// Keeps an assigned entry ID while bringing it to the desired path. If the key is empty or
        /// no longer exists, adopts an entry already at the path or creates a new one.
        /// </summary>
        public I18nEditorEntry EnsureEntry(I18nKey key, string path)
        {
            EnsureActive();
            if (key.IsAssigned)
            {
                I18nEntry? existingById = _catalog.FindById(key.Id);
                if (existingById != null)
                {
                    I18nEditResult move = _catalog.MoveEntry(key.Id, path);
                    I18nEntry movedEntry = RequireSuccess(move);
                    HasChanges |= move.HasChanges;
                    return new I18nEditorEntry(this, ParseId(movedEntry), false);
                }
            }

            return GetOrCreateEntry(path);
        }

        /// <summary>Finds an entry by stable key.</summary>
        public I18nEditorEntry? FindById(I18nKey key)
        {
            EnsureActive();
            if (!key.IsAssigned)
            {
                return null;
            }

            I18nEntry? entry = _catalog.FindById(key.Id);
            return entry == null ? null : new I18nEditorEntry(this, key.Id, false);
        }

        /// <summary>Finds an entry by logical path.</summary>
        public I18nEditorEntry? FindByPath(string path)
        {
            EnsureActive();
            I18nEntry? entry = _catalog.FindByPath(path);
            return entry == null
                ? null
                : new I18nEditorEntry(this, ParseId(entry), false);
        }

        internal I18nEntry GetEntry(long id)
        {
            EnsureActive();
            I18nEntry? entry = _catalog.FindById(id);
            if (entry == null)
            {
                throw new I18nEditorException($"Localization entry with ID '{id}' no longer exists.");
            }

            return entry;
        }

        internal void SetComment(long id, string? comment)
        {
            Apply(_catalog.SetEntryComment(id, comment));
        }

        internal void SetText(long id, string localeId, string? text)
        {
            Apply(_catalog.SetEntryText(id, localeId, text));
        }

        internal void SetAsset(long id, string localeId, I18nAssetReference? asset)
        {
            Apply(_catalog.SetEntryAsset(id, localeId, asset));
        }

        internal void Move(long id, string path)
        {
            Apply(_catalog.MoveEntry(id, path));
        }

        internal void Remove(long id)
        {
            Apply(_catalog.RemoveEntry(id));
        }

        internal void Complete()
        {
            _isComplete = true;
        }

        private void Apply(I18nEditResult result)
        {
            EnsureActive();
            RequireSuccess(result);
            HasChanges |= result.HasChanges;
        }

        private static I18nEntry RequireSuccess(I18nEditResult result)
        {
            if (result.IsSuccess)
            {
                return result.Entry!;
            }

            throw new I18nEditorException(result.Error!.Message);
        }

        private static long ParseId(I18nEntry entry)
        {
            return long.Parse(entry.Id, CultureInfo.InvariantCulture);
        }

        private void EnsureActive()
        {
            if (_isComplete)
            {
                throw new I18nEditorException(
                    "This catalog edit has completed. Entry handles cannot be modified outside I18nEditor.Edit().");
            }
        }
    }
}
