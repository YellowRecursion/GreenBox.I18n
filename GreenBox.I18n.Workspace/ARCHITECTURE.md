# Catalog Workspace architecture

`GreenBox.I18n.Workspace` is the application layer between pure catalog rules and local adapters. It is the single owner of an open catalog working copy.

```text
Web -> Editor Host -> Catalog Workspace -> Core
MCP -> Editor Host -> Catalog Workspace -> Core
```

`Core` owns the catalog model, business rules, serialization, validation, merge, and MF2. It has no knowledge of files, Web, MCP, Unity, or sessions.

`Catalog Workspace` owns:

- the active catalog path and in-memory working copy;
- the source baseline and content hash;
- monotonically increasing revisions;
- dirty entry, locale, and path tracking;
- external-source detection;
- paged, revision-bound queries;
- short-lived prepared change sets;
- validation and atomic persistence when a prepared change set is applied.

Host-specific concerns stay outside this project: HTTP, personal preferences, file pickers, Unity process presence, the SQLite usage index, and Unity navigation. The Host supplies trustworthy usage counts when preparing deletions; the Workspace treats missing usage information as unknown and blocks unsafe deletion.

## Concurrency contract

Every query cursor and prepared change set belongs to one workspace revision. Any Web edit, save, reopen, merge, or other applied batch invalidates stale operations. Applying also rechecks the source content hash immediately before writing. Therefore editing the JSON or Web working copy while an MCP operation is in progress causes a clear conflict instead of silent overwrite.

MCP writes are deliberately blocked while Web has unsaved changes. This avoids guessing how two concurrent user intentions should be merged.

## Prepared changes

Preparation clones the working copy, applies a complete batch in memory, validates the result, and returns a summary, entry-level preview, warnings, blockers, and diagnostics. It does not write the source file.

Apply accepts only the opaque `changeSetId`. After all concurrency checks pass, it serializes once, replaces the source atomically, updates the baseline, advances the revision, and invalidates all other prepared change sets.
