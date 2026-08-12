# Desktop architecture

`GreenBox.I18n.Desktop` is the Windows composition root for the local tools.

- Normal launch shows the application console, starts Host in-process, and opens Web in the
  default browser. Closing or minimizing hides the window in the tray; explicit **Exit** stops the
  owned Host.
- `mcp` mode starts Host when necessary and then runs the STDIO MCP server. stdout belongs only to
  MCP protocol messages.
- `host` mode exposes the same embedded Host for diagnostics and production smoke tests.
- CLI remains a separate console executable in the same Desktop bundle. The installer creates a
  stable `i18n.cmd` outside the replaceable `current` folder and adds it to the user's PATH.

Web, Host, CLI, and MCP are built and updated as one bundle. They may remain separate projects for
clear dependencies and independent tests, but users never have to match their versions manually.

Velopack installs per user, downloads updates in the background, and replaces the complete
`current` directory only after an explicit exit/restart. Persistent user settings must not be
stored in `current` because that directory is replaced on every update.
