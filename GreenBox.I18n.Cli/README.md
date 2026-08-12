# GreenBox I18n CLI

The CLI performs small, deterministic catalog operations. Rich project analysis and AI-oriented workflows belong to MCP rather than this tool.

On Windows, installing GreenBox Desktop Tools also installs the `i18n` command and keeps it on the
same version as Web, Host, and MCP. Automation environments can instead install the
`GreenBox.I18n.Cli` .NET Tool package.

## Commands

| Command | Purpose |
|---|---|
| `validate <catalog>` | Validate the complete catalog. |
| `get <catalog> <id>` | Read one entry. |
| `search <catalog> <query>` | Find entries by path, comment, or localized text. |
| `generate-id [count]` | Generate stable entry IDs. |
| `add <catalog> <path>` | Create an empty entry. |
| `update <catalog> <id> [options]` | Set or clear comment, localized text, and asset reference. |
| `move <catalog> <id> <path>` | Rename or move an entry while preserving its ID. |
| `remove <catalog> <id>` | Delete an entry. |
| `merge <base> <current> <incoming> <output>` | Perform the catalog's three-way merge. |
| `git install` | Configure the Git merge driver on the current computer. |

Use `--json` on catalog commands when stable machine-readable output is needed.

## Update examples

```text
i18n update localization.json 3857333080842834967 --comment "Shown above reports"
i18n update localization.json 3857333080842834967 --locale en --text "Reports"
i18n update localization.json 3857333080842834967 --locale en --asset-guid 0123456789abcdef0123456789abcdef --asset-local-id 21300000
i18n update localization.json 3857333080842834967 --locale en --clear-text --clear-asset
```

Several compatible changes may be supplied together. The catalog is written once and only after the complete result passes validation.
