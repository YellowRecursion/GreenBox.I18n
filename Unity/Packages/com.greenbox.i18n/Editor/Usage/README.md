# Usage scanning architecture

The usage scanner is split by responsibility so that file analysis can eventually run on worker
threads without moving Unity APIs off the main thread.

## Modules

```text
GreenBox.I18n.Unity.Editor (Unity Integration)
    -> GreenBox.I18n.Usage.Cecil
        -> GreenBox.I18n.Usage.Analysis
    -> GreenBox.I18n.Usage.Analysis
    -> GreenBox.I18n.Usage.Index
        -> GreenBox.I18n.Usage.Analysis

GreenBox.I18n.Usage.Analysis
    -> GreenBox.I18n.Core (entry ID validation only)
```

`A -> B` means that A knows about and calls B. B does not know about A.

### Usage Analysis

`Analysis/GreenBox.I18n.Usage.Analysis.asmdef` has `noEngineReferences` enabled. It contains
thread-agnostic contracts and utilities: usage/result models, path handling, profiling, source
stamps, the raw DLL prefilter, and serialized asset analysis. Asset analysis is split into a
streaming marker prefilter, Unity YAML parser, serialized object model, property-path builder, and
result models. It must not reference UnityEngine, UnityEditor, Mono.Cecil, settings, logs, static
events, or mutable Unity state.

### Usage Cecil

`Cecil/GreenBox.I18n.Usage.Cecil.asmdef` also has `noEngineReferences` enabled. It is an
infrastructure adapter that translates Mono.Cecil data into Usage Analysis result models. Keeping
it separate prevents the central analysis contracts from depending on Cecil.

### Unity Integration

The files directly under `Usage/` are part of `GreenBox.I18n.Unity.Editor`. This layer owns
Unity callbacks, `AssetDatabase`, `CompilationPipeline`, `SessionState`, preferences, logging,
scan coordination, and result publication. Only this layer decides when a scan runs and whether a
result is still current enough to publish. It also resolves script GUIDs through `AssetDatabase`
after pure YAML analysis; this keeps Unity calls on the main thread while allowing the expensive
file work to move to a worker later.

### Usage Index

`Index/GreenBox.I18n.Usage.Index.asmdef` has `noEngineReferences` enabled. It accepts immutable
Usage Analysis results and persists them without knowing when Unity scans run. The index is a
derived local cache stored at `Library/GreenBox.I18n/usage-index.db`; it is never project content
and can always be rebuilt by a full scan.

The index uses SQLite in WAL mode and is split into four responsibilities:

- `I18nUsageIndexStore` owns index state and full or partial transactions;
- `I18nUsageIndexSourceWriter` replaces rows owned by one asset or assembly;
- `I18nUsageIndexSchema` owns the versioned schema;
- the small `I18nSqlite` adapter talks to the SQLite library supplied by the operating system, so
  the Unity package does not ship machine-specific managed or native binaries.

Every source is stored even when it has zero usages. A successful source replacement deletes only
that source's previous rows and inserts its new snapshot in the same transaction. A failed source
keeps its previous usage rows and records the error. A changed source stays pending because its
scan result is intentionally discarded. Deleting a source cascades to its usage rows.

`I18nUsageIndexController` belongs to Unity Integration. It is the only bridge between scanner
lifecycle events, preferences, automatic retries, and the storage module. Initialization is
silent unless the database cannot be opened or migrated.

## Source changes during a scan

Scanning uses optimistic consistency rather than locking Unity assets or compiler output:

1. Unity Integration assigns a monotonically increasing in-memory revision to every notified
   source change.
2. A partial scan captures those revisions before analysis.
3. The scanners capture cheap file stamps before reading and compare them after analysis. An asset
   stamp includes its `.meta`; an IL stamp includes its `.pdb`.
   Full asset scans also compare the discovered file set at the end, so a file created or deleted
   while scanning invalidates the result.
4. Before publishing, Unity Integration compares the captured revisions with the current ones.
5. If either the revision or file stamp changed, that source result is marked `Changed` and is not
   published. Automatic mode queues the affected source again; manual mode asks the developer to
   rerun the scan.

File stamps are consistency guards, not cache keys. They deliberately avoid hashing or rereading
content. Cancellation can later reduce wasted work, but correctness must continue to rely on the
final revision and stamp checks.

Every independently replaceable asset or assembly produces its own immutable source result:

- `Success` is publishable, including the important case of zero usages;
- `Failed` carries an error and must not erase the last known usages for that source;
- `Changed` was invalidated while scanning and must not be published.

A partial batch is still published to index consumers when some sources changed. Consumers apply
only its `Success` and `Failed` results, while Unity Integration queues the `Changed` sources again
in Automatic mode. A full scan remains atomic: it replaces the complete index only when every
source is stable and the global source revision has not advanced.

When scans become asynchronous, pending work must have explicit `Pending` and `InFlight` states.
Work is removed only after a stable result is published. A full scan should build a staging
snapshot, replay changes received while it was running, and then atomically replace the published
index.

## Threading rules

- Analysis and Cecil code must not call Unity APIs or read mutable Unity state.
- Unity metadata is captured on the main thread and passed to analysis as immutable data.
- Analysis remains synchronous; Unity Integration chooses whether to invoke it directly or on a
  worker thread.
- Results are immutable after construction.
- Unity objects, logging, settings, and result publication stay on the main thread.
