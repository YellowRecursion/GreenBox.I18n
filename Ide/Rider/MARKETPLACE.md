# JetBrains Marketplace release

The Rider plugin has its own update channel. It is packaged and versioned with GreenBox I18n, but
publishing it does not rebuild or publish Desktop, Unity, CLI, or MCP.

## First publication

JetBrains requires the first upload and vendor setup in the Marketplace UI:

1. Build the ZIP with `./eng/Build-Rider.ps1` from the repository root.
2. Create or select the GreenBox vendor on JetBrains Marketplace.
3. Upload `artifacts/rider/<version>/greenbox-i18n-rider-<version>.zip` as a new plugin.
4. Keep the default release channel so Rider can install and update the plugin normally.
5. Use `https://github.com/YellowRecursion/GreenBox.I18n` as the source repository and issue
   tracker base.
6. Select Apache License 2.0. The plugin is published free of charge.

Official instructions: <https://plugins.jetbrains.com/docs/marketplace/uploading-a-new-plugin.html>

## Updates

Create a permanent Marketplace token once and keep it outside the repository. For each update:

1. increment `eng/version.txt` through the normal GreenBox release flow;
2. update `<change-notes>` in `src/main/resources/META-INF/plugin.xml`;
3. build and verify the plugin locally;
4. publish it with:

```powershell
$env:PUBLISH_TOKEN = 'marketplace-token'
.\eng\Publish-Rider.ps1
```

The token is read only from the current process environment and must never be committed. Existing
users receive the new compatible version through Rider's standard plugin updater; no settings need
to be changed on their computers.

The plugin currently declares compatibility with Rider 2025.2 (`252.*`). Expand that range only
after a build or Plugin Verifier check against the additional Rider version.
