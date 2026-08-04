#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace GreenBox.I18n.Unity.Editor.Pickers
{
    /// <summary>
    /// Displays localization entries as a searchable path tree.
    /// </summary>
    internal sealed class I18nKeyDropdown : AdvancedDropdown
    {
        private readonly I18nCatalog? _catalog;
        private readonly Action<long> _selectionHandler;

        /// <summary>
        /// Initializes a localization key dropdown.
        /// </summary>
        /// <param name="catalog">Catalog whose entries can be selected.</param>
        /// <param name="selectionHandler">Callback invoked with the selected stable ID.</param>
        internal I18nKeyDropdown(I18nCatalog? catalog, Action<long> selectionHandler)
            : base(new AdvancedDropdownState())
        {
            _catalog = catalog;
            _selectionHandler = selectionHandler;
            minimumSize = new Vector2(320f, 320f);
        }

        /// <inheritdoc />
        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("Localization Keys");
            root.AddChild(new KeyItem("none", 0));

            if (_catalog == null)
            {
                return root;
            }

            root.AddSeparator();
            var groups = new Dictionary<string, AdvancedDropdownItem>(StringComparer.Ordinal);

            for (int entryIndex = 0; entryIndex < _catalog.Entries.Count; entryIndex++)
            {
                I18nEntry entry = _catalog.Entries[entryIndex];
                if (!long.TryParse(
                        entry.Id,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out long id) ||
                    id <= 0)
                {
                    continue;
                }

                AddEntry(root, groups, entry.Path, id);
            }

            return root;
        }

        /// <inheritdoc />
        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item is KeyItem keyItem)
            {
                _selectionHandler(keyItem.KeyId);
            }
        }

        private static void AddEntry(
            AdvancedDropdownItem root,
            IDictionary<string, AdvancedDropdownItem> groups,
            string path,
            long id)
        {
            string[] segments = path.Split('.');
            AdvancedDropdownItem parent = root;
            string groupPath = string.Empty;

            for (int segmentIndex = 0; segmentIndex < segments.Length - 1; segmentIndex++)
            {
                string segment = segments[segmentIndex];
                groupPath = groupPath.Length == 0 ? segment : $"{groupPath}.{segment}";

                if (!groups.TryGetValue(groupPath, out AdvancedDropdownItem group))
                {
                    group = new AdvancedDropdownItem(segment);
                    parent.AddChild(group);
                    groups.Add(groupPath, group);
                }

                parent = group;
            }

            parent.AddChild(new KeyItem(path, id));
        }

        private sealed class KeyItem : AdvancedDropdownItem
        {
            /// <summary>
            /// Initializes a selectable localization key item.
            /// </summary>
            internal KeyItem(string path, long keyId)
                : base(GetDisplayName(path))
            {
                KeyId = keyId;

                // AdvancedDropdown renders the content created by its constructor, but searches
                // the public name property. Keeping the complete path here makes every path
                // segment searchable without repeating the path in the visible tree leaf.
                name = path;
            }

            /// <summary>
            /// Gets the stable localization entry ID represented by this item.
            /// </summary>
            internal long KeyId { get; }

            private static string GetDisplayName(string path)
            {
                int separatorIndex = path.LastIndexOf('.');
                return separatorIndex < 0 ? path : path.Substring(separatorIndex + 1);
            }
        }
    }
}
