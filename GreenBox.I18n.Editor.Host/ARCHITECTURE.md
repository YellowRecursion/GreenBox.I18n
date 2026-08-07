# Editor host architecture

The host is the local authority for an editor session:

- `Editor` owns the mutable catalog working copy and its revision.
- `Endpoints` translate HTTP requests into editor operations.
- `Contracts` define JSON request and response shapes.
- `Infrastructure` contains file-system, process, Unity-project discovery, and read-only usage-index integration.
- `Program.cs` only configures dependency injection and maps endpoints.

Catalog rules, mutations, serialization, and validation remain in `GreenBox.I18n.Core`. The host coordinates those operations but must not duplicate them.

The Unity package owns and writes `Library/GreenBox.I18n/usage-index.db`. The host discovers that database from the open catalog and opens a short-lived read-only SQLite connection per request. Web clients consume `/api/usage-index`; they never access the database directly. Entry IDs and Unity local IDs are JSON strings because their 64-bit values are not safe JavaScript numbers.

Unity holds `Library/GreenBox.I18n/unity-editor.lock` with write sharing disabled while the Editor is running. The host probes that project-scoped lease through `/api/unity-project`; the operating system releases it after both a normal shutdown and a process crash. Presence and usage-index state remain separate concerns.
