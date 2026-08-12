# Releasing GreenBox I18n

This is the operational checklist for frequent beta patches. Versioning, packaging, and smoke
tests are scripted so a patch does not require manually assembling files.

## Version ownership

`eng/version.txt` is the product version source. `eng/Set-Version.ps1` also updates Unity's
`package.json`; .NET assemblies and the Rider plugin read the same version during their builds.

## Build locally

Prerequisites: Windows, .NET 10 SDK, Node.js/npm, PowerShell, and an installed Rider. Set
`RIDER_HOME` only if the script cannot find Rider under `Program Files\JetBrains`.

```powershell
.\eng\Build-Release.ps1 `
  -Version 0.1.0-beta.2 `
  -UpdateSource https://github.com/YellowRecursion/GreenBox.I18n `
  -ReleaseNotes .\RELEASE_NOTES.md
```

The script performs these steps:

1. updates and validates the shared version;
2. rebuilds the Core DLL embedded in the Unity package;
3. runs the .NET test suite;
4. builds the production Web editor and self-contained Windows Desktop bundle;
5. packages the CLI as a .NET Tool;
6. builds the Rider plugin ZIP against the installed Rider SDK;
7. creates the installer/update feed and runs real CLI, Host, Web, and MCP smoke tests.

Outputs:

- `artifacts/releases/beta/GreenBox.I18n-beta-Setup.exe`;
- `artifacts/releases/beta/GreenBox.I18n-beta-Portable.zip`;
- `artifacts/nuget/GreenBox.I18n.Cli.<version>.nupkg`;
- `artifacts/rider/<version>/*.zip`;
- generated Unity Core binaries under the tracked Unity package.

Use `-SkipTests` only while iterating on packaging, never for a published build. Use
`-DownloadPrevious` in CI or when the previous GitHub beta should be downloaded to produce delta
updates.

## Publish a patch

1. Update `RELEASE_NOTES.md`.
2. Run the local release command above.
3. Review and commit the version, source, and generated Unity Core binary changes.
4. Create and push the matching tag, for example `v0.1.0-beta.2`.

The `release-beta.yml` workflow verifies that the tag equals `eng/version.txt`, downloads the
previous beta for delta generation, rebuilds and tests everything, checks that generated Unity
binaries were committed, then publishes a GitHub prerelease. Desktop installations discover it
automatically.

For an emergency manual publication after a successful build:

```powershell
$env:GITHUB_TOKEN = '...'
.\eng\Publish-GitHub-Beta.ps1 `
  -RepositoryUrl https://github.com/YellowRecursion/GreenBox.I18n `
  -Tag v0.1.0-beta.2
```

Without `-Draft`, this publishes immediately. Do not reuse a released version; increment the beta
patch instead.

## Independent channels

- **Desktop Tools:** GitHub prereleases through Velopack. One update replaces Web, Host, CLI, and
  MCP atomically.
- **Unity:** repository tag through Unity Package Manager. The package and Desktop may update at
  different times, so their communication must remain compatibility-aware.
- **Rider:** JetBrains Marketplace. Publishing needs a Marketplace vendor/plugin registration and
  token; users receive updates through Rider.
- **Standalone CLI:** NuGet.org after a package ID and API key are registered. Desktop users do not
  need this package.

## Before the first public beta

- replace documentation placeholders with the public GitHub repository URL;
- obtain a Windows code-signing certificate and pass signing settings to the packaging workflow;
- create the JetBrains Marketplace listing and add its publishing secret;
- reserve `GreenBox.I18n.Cli` on NuGet.org and add its publishing secret.

Unsigned builds are suitable for the current internal beta but produce a SmartScreen warning.
