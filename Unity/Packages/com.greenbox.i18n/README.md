# GreenBox I18n for Unity

GreenBox I18n provides the Unity runtime and editor integration for a GreenBox localization project.

## Getting started

1. Install the `com.greenbox.i18n` package.
2. Open the Unity project.

GreenBox I18n creates and configures the project catalog automatically:

```text
Assets/GreenBox.I18n/
├── localization.json
├── readme.md
└── Resources/
    └── greenbox-i18n.asset
```

No runtime initialization is required:

```csharp
string text = I18n.Text(entryId);
```

The initial locale is selected from the device language. An explicit selection is remembered:

```csharp
I18n.SetLocale("ru");
```

Clear the saved selection to return to automatic device-language selection:

```csharp
I18n.ClearLocaleOverride();
```

Subscribe when UI outside the built-in GreenBox components needs to react:

```csharp
I18n.LocaleChanged += locale => RefreshUi(locale.Id);
```

The `GreenBox.I18n` folder may be moved as a whole inside `Assets`. Keep its name and managed internal structure unchanged.
