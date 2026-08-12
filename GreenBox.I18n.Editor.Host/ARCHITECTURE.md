# Editor host architecture

The host is the local process authority for editor integrations:

- `GreenBox.I18n.Workspace` owns the mutable catalog working copy, revision, queries, and prepared writes.
- `Endpoints` translate HTTP requests into editor operations.
- `Contracts` define JSON request and response shapes.
- `Infrastructure` contains file-system, process, Unity-project discovery, and read-only usage-index integration.
- `Program.cs` only configures dependency injection and maps endpoints.

Catalog rules, mutations, serialization, and validation remain in `GreenBox.I18n.Core`. The Workspace coordinates them, while the Host adds HTTP, personal preferences, Unity discovery, usage-index access, and navigation. MCP calls the Host rather than opening JSON or SQLite itself, so Web and MCP always observe one working copy.

The Unity package owns and writes `Library/GreenBox.I18n/usage-index.db`. The host discovers that database from the open catalog and opens a short-lived read-only SQLite connection per request. Web clients consume `/api/usage-index`; they never access the database directly. Entry IDs and Unity local IDs are JSON strings because their 64-bit values are not safe JavaScript numbers.

`/api/usage-index/state` is the cheap polling surface. Clients load the full per-entry summary only when its successful-update timestamp changes, so a large catalog is not repeatedly serialized while idle.

Unity holds `Library/GreenBox.I18n/unity-editor.lock` with write sharing disabled while the Editor is running. The host probes that project-scoped lease through `/api/unity-project`; the operating system releases it after both a normal shutdown and a process crash. Presence and usage-index state remain separate concerns.

Usage navigation is also project-scoped. The browser posts only an entry ID and an opaque location ID. The host re-reads the current index, verifies that the location still belongs to the entry, and publishes a short-lived command under `Library/GreenBox.I18n/Bridge`. Unity consumes it on the main thread and writes a bounded response. Paths supplied by the browser are never forwarded to the operating system or Unity.
