# GreenBox I18n

GreenBox I18n is a Unity localization runtime and toolchain. One catalog is shared by the Unity
package, Web editor, CLI, MCP server, and Rider integration.

The project is currently in beta and targets Windows and Unity 6.

## Install

- **Desktop Tools:** download and run `GreenBox.I18n-beta-Setup.exe` from the latest beta release.
  It installs Web, Host, CLI, and MCP together and keeps them on one version.
- **Unity:** add the Git package URL ending in
  `?path=/Unity/Packages/com.greenbox.i18n#v<version>` through Unity Package Manager.
- **Rider:** install GreenBox I18n from JetBrains Marketplace when the beta listing becomes
  available.

See [INSTALL.md](INSTALL.md) for the complete one-time setup and update behavior.

## Repository layout

| Area | Purpose |
|---|---|
| `GreenBox.I18n.Core` | Catalog model, MF2 profile, validation, compilation, and runtime data. |
| `Unity/Packages/com.greenbox.i18n` | Unity runtime and Editor integration. |
| `GreenBox.I18n.Editor.Web` | Browser UI. |
| `GreenBox.I18n.Editor.Host` | Local catalog owner and HTTP API. |
| `GreenBox.I18n.Desktop` | Windows app, tray lifecycle, installer, and updates. |
| `GreenBox.I18n.Cli` | Small deterministic CRUD and merge commands. |
| `GreenBox.I18n.Mcp` | Rich, guarded LLM workflows over the Host working copy. |
| `Ide/Rider` | Rider inlay hints for entry IDs. |

## Build a beta

The complete local release is one command:

```powershell
.\eng\Build-Release.ps1 -Version 0.1.0-beta.2 -UpdateSource https://github.com/OWNER/REPOSITORY -ReleaseNotes .\RELEASE_NOTES.md
```

See [eng/RELEASING.md](eng/RELEASING.md) before publishing a release.
