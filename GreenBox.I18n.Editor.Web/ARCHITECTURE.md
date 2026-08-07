# Web editor architecture

The web editor uses a small layered structure inspired by Feature-Sliced Design:

`app -> widgets -> features -> entities -> shared`

- `app` composes application-wide providers and the root view.
- `widgets` assemble complete areas of the editor UI.
- `features` contain user actions such as moving or renaming catalog entries. The folder is added when the first feature is implemented.
- `entities` contain catalog-facing state, API calls, and domain presentation logic.
- `shared` contains generic infrastructure without catalog-specific behavior.
- `design` owns the editor's visual tokens and Ant Design theme.

A lower layer must not import from a higher layer. Ant Design components are used directly unless a wrapper adds editor-specific behavior.

The .NET host owns the mutable catalog working copy. The browser reads state and sends commands through `/api`; it does not independently reproduce Core mutations or validation.

Unity project presence is an entity supplied by `/api/unity-project`. The Web editor polls it as ephemeral status and keeps the last known value across transient Host failures; it does not inspect Unity files or infer process state itself.

Usage counts are shown only while Unity is online and `/api/usage-index` reports a ready index with no failed sources. An unavailable or updating index contributes no zeroes or warnings to the hierarchy. Folder warning state is derived from descendant entry counts in the catalog tree model.

The Web editor polls only `/api/usage-index/state`. It downloads and rebuilds the full per-entry count map when `updatedAtUtc` changes, avoiding repeated O(entries) work for idle projects with thousands of entries.

An entry usage row carries an opaque `locationId`. Clicking it sends that ID back to the Host; the browser never decides which local file or Unity object should be opened.
