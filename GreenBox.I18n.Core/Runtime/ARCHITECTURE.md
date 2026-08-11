# Compiled catalog runtime

The source catalog and the runtime catalog deliberately use different representations.

```text
I18nCatalog JSON model
        |
        v
I18nCompiledCatalogCompiler
        |
        v
I18nCompiledCatalogBuilder
        |
        v
I18nCompiledCatalogStorage <-> versioned binary
        |
        v
I18nRuntime
```

The source model uses ordinary objects and dictionaries because it is edited and validated. The
compiled model is immutable and uses shared arrays of records because it is read much more often
than it is created.

## Responsibilities

- `I18nCompiledCatalogCompiler` validates source data and compiles MF2 messages.
- `I18nCompiledCatalogBuilder` pools strings and lowers temporary compiler objects into records.
- `I18nCompiledCatalogStorage` owns the final arrays and verifies all indexes and ranges once.
- `I18nCompiledCatalogBinary` only persists and restores storage. It contains no business rules.
- `I18nRuntime` searches the immutable arrays directly and never builds per-entry collections.
- `I18nCompiledMessage.CatalogStorage` contains the single MF2 executor. Lightweight views let the
  same executor read both a temporary parser result and the flat runtime storage, preventing the two
  paths from developing different formatting semantics.

## Invariants

- Entries are sorted by ID and are found with binary search.
- An entry owns one contiguous, locale-sorted range of values.
- Locale fallback chains are prepared by the builder and contain the default locale.
- Strings are stored once in the catalog string table; records contain string indexes.
- Number-format options are interned once; numeric parts and selectors contain option indexes.
- Missing optional values use `I18nCompiledCatalogFormat.MissingIndex`.
- Runtime code must not mutate storage or retain the editable source model.
- The binary reader validates data before an `I18nCompiledCatalog` becomes observable.

`BinaryVersion` changes when the file layout changes. `CompilerVersion` changes when generated
runtime semantics change without changing the layout. Unity cache hashes include both through
`I18nCompiledCatalogBinary.CompilerFingerprint`.
