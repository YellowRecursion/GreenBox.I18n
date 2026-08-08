# GreenBox I18n for Unity

GreenBox I18n provides the Unity runtime and editor integration for a GreenBox localization project.

## Getting started

1. Install the `com.greenbox.i18n` package.
2. Open the Unity project.

GreenBox I18n creates and configures the project catalog automatically:

```text
Assets/GreenBox.I18n/
├── localization.json
├── localization.asset
└── readme.md
```

The folder and files may be moved or renamed in Unity. Their relationships are tracked by Unity GUIDs rather than by the default paths.

The active project catalog is managed automatically. Its current location and recovery controls are available in **Edit → Project Settings → GreenBox → i18n**.
