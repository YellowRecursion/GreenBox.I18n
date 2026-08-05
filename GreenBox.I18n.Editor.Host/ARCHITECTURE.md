# Editor host architecture

The host is the local authority for an editor session:

- `Editor` owns the mutable catalog working copy and its revision.
- `Endpoints` translate HTTP requests into editor operations.
- `Contracts` define JSON request and response shapes.
- `Infrastructure` will contain file-system and process integration when those behaviors are introduced.
- `Program.cs` only configures dependency injection and maps endpoints.

Catalog rules, mutations, serialization, and validation remain in `GreenBox.I18n.Core`. The host coordinates those operations but must not duplicate them.
