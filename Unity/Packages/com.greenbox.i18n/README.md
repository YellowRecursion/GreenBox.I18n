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
The `Resources` folder contains generated runtime data and is intentionally excluded from Git.

## Safe Git merges

After installing GreenBox Desktop Tools, open `Preferences > GreenBox > i18n`. The integration can be configured there or with:

```text
i18n git install
```

The command registers the merge driver once for the current user. Every GreenBox project opts into it through its generated `.gitattributes`, so Git merges `localization.json` structurally by stable locale and entry IDs instead of relying on JSON line positions.
