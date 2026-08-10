# GreenBox I18n Rider plugin

This directory contains the GreenBox I18n integration for JetBrains Rider. It reads the project
source catalog path from `ProjectSettings/GreenBox.I18n.asset` and renders the final path segment
beside every known entry ID in C# source. It does not modify the source document.

Semantic C# call detection is deliberately outside the current implementation; known IDs are
recognized as numeric tokens in C# documents.

## Run in a sandbox Rider

Set `RIDER_HOME` to the Rider installation directory and run:

```powershell
$env:RIDER_HOME = 'C:\Program Files\JetBrains\JetBrains Rider 2025.1.4'
$env:JAVA_HOME = "$env:RIDER_HOME\jbr"
.\gradlew.bat runIde
```

The sandbox stores its settings separately from the regular Rider instance.

## Build an installable plugin

```powershell
.\gradlew.bat buildPlugin
```

The plugin ZIP is written to `build/distributions`.
