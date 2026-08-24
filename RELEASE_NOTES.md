# GreenBox I18n beta

- Unity localization components now react to `OnValidate` only while their GameObject is selected,
  preventing unrelated scene and prefab diffs.
- Edit Mode localization previews now always use the catalog default locale instead of retaining a
  locale selected by the runtime.
- Significantly faster Web editor hierarchy expansion, scrolling, and search for large catalogs.
- New GreenBox.I18n branding across Desktop Tools, Web, Unity components, and Rider.
- Native locale-aware browser spellcheck for translation fields.
- Unity components now refresh directly through their runtime lifecycle, persist Edit Mode previews,
  respect their enabled state, and can be refreshed manually or after catalog compilation.
- Improved Unity diagnostics for unassigned keys and ID search in the I18nKey picker.
- Unity usage indexing now recognizes I18nKey values serialized by Odin Inspector.
- MCP can report incomplete localization and gives LLM clients concise workspace guidance.
- Fixed Rider entry-name inlays not attaching to restored C# editors.
