# Installing GreenBox I18n Beta

## Desktop Tools

1. Download `GreenBox.I18n-beta-Setup.exe` from the latest beta release.
2. Run it. The per-user installer does not require administrator rights.
3. GreenBox I18n opens its console window and the Web editor. Closing or minimizing the console
   sends it to the system tray; use **Exit** in the tray menu to stop it.

The installation contains Web, Host, CLI, and MCP. Do not install or update those four parts
separately unless you are developing GreenBox itself.

The installer adds the `i18n` command to the current user's PATH. Open a new terminal after the
first installation. Existing terminals keep their old PATH.

### Updates

Desktop Tools checks its beta GitHub release channel in the background. A downloaded update is
shown in the console window and is applied only when you choose **Restart to update** or exit the
application. The editor is never restarted in the middle of work without an explicit action.

## Unity package

In Unity Package Manager choose **Add package from git URL** and use:

```text
https://github.com/YellowRecursion/GreenBox.I18n.git?path=/Unity/Packages/com.greenbox.i18n#v0.1.0-beta.1
```

Change the tag suffix to the desired release. GreenBox creates the project catalog and generated
runtime asset when Unity opens.

To update, change only the tag after `#` to the newer release. Catalog data remains in the Unity
project and is not owned by the package cache.

## Git merge integration

After Desktop Tools is installed, open **Edit > Preferences > GreenBox > i18n** in Unity and choose
the configure action. This is a one-time setting per computer, not per project. It can also be run
from a new terminal:

```powershell
i18n git install
```

Projects already contain the required `.gitattributes` rule. Registration is intentionally
explicit because it changes the user's global Git configuration.

## MCP

MCP client configuration is also one-time. Use STDIO with:

```text
Command: %LOCALAPPDATA%\GreenBox.I18n\GreenBox.I18n.exe
Arguments: mcp
```

The stable launcher survives Desktop updates. It starts the local Host when needed, so there is no
second server to launch manually and no MCP setting to change after an update.

## CLI without Desktop

The Desktop installer is the recommended CLI distribution on Windows. A standalone .NET Tool
package is also produced for automation environments:

```powershell
dotnet tool install --global GreenBox.I18n.Cli --prerelease
dotnet tool update --global GreenBox.I18n.Cli --prerelease
```

## Rider

The Rider plugin uses JetBrains Marketplace as a separate release channel. Rider handles plugin
updates itself. Updating Desktop Tools or the Unity package does not replace the Rider plugin.

## Beta signing note

Development beta installers are currently unsigned, so Windows SmartScreen can warn before the
first run. Public distribution should start only after the release workflow is supplied with a
code-signing certificate.
