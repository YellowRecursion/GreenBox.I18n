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
