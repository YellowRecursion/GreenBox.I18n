# GreenBox I18n for Unity

<img src="Documentation~/greenbox-i18n-icon.png" alt="GreenBox I18n" width="96">

GreenBox I18n provides the Unity runtime and editor integration for a GreenBox localization project.

Licensed under the Apache License 2.0. See [LICENSE.md](LICENSE.md).

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

Locale metadata and its optional Unity sprite are available without maintaining a second locale list:

```csharp
foreach (I18nRuntimeLocale locale in I18n.Locales)
{
    string label = locale.DisplayName;
    Sprite? icon = locale.GetIcon();
}
```

The `GreenBox.I18n` folder may be moved as a whole inside `Assets`. Keep its name and managed internal structure unchanged.
The `Resources` folder contains generated binary runtime data and is intentionally excluded from Git. The player does not read `localization.json` or parse MessageFormat at startup.

## Safe Git merges

After installing GreenBox Desktop Tools, open `Preferences > GreenBox > i18n`. The integration can be configured there or with:

```text
i18n git install
```

The command registers the merge driver once for the current user. Every GreenBox project opts into it through its generated `.gitattributes`, so Git merges `localization.json` structurally by stable locale and entry IDs instead of relying on JSON line positions.

## Generating entries from Unity Editor tools

Editor scripts can create or update many entries as one validated catalog transaction:

```csharp
using GreenBox.I18n.Unity.Editor;
using UnityEditor;

I18nKey key = I18nEditor.Edit(edit =>
{
    I18nEditorEntry entry = edit.EnsureEntry(
        existingKey,
        "Cosmetics.Hat.Name");

    if (entry.WasCreated)
    {
        entry.SetDefaultText("Hat");
    }

    entry.SetComment("Display name of the hat cosmetic item.");
    return entry.Key;
});

Undo.RecordObject(cosmetic, "Assign localization key");
cosmetic.NameKey = key;
EditorUtility.SetDirty(cosmetic);
```

`EnsureEntry` preserves an existing stable ID when the desired path changes. An empty or missing key adopts an entry already at that path or creates a new one. Put loops inside one `I18nEditor.Edit` call so the source file is written and imported only once.

Editor tools use the same text API as the game:

```csharp
string defaultText = I18n.Text(task.DescriptionKey);
string progress = I18n.Text(task.ProgressKey, ("count", completedCount));

Sprite defaultImage = I18nEditor.Asset<Sprite>(task.ImageKey);
Sprite russianImage = I18nEditor.Asset<Sprite>(task.ImageKey, localeId: "ru");
```

In Edit Mode, `I18n.Text` uses the catalog default locale. In Play Mode, it uses the active game locale. Both modes read the generated `localization.asset`, and the Editor invalidates the cached runtime automatically after catalog compilation. Unlike `I18nEditor.Edit`, localization reads do not parse or write `localization.json`.

## UI Toolkit

UI Toolkit elements are stored in a `UIDocument` visual tree rather than as GameObject components. Bind the property that displays localized content to a GreenBox entry in UI Builder:

1. Select a `Label`, `Button`, or another text element and add an `I18nTextBinding` to its `text` property.
2. Enter the entry's stable numeric ID in **Entry Id**.
3. For a localized sprite, add an `I18nSpriteBinding` to a `VisualElement`'s `style.backgroundImage` property or an `Image` element's `sprite` property.

The bindings use the same compiled catalog and current locale as `I18nText` and `I18nImage`. They refresh when `I18n.SetLocale` changes the locale. No second localization table or `UIDocument` companion component is needed.

The equivalent UXML can be written directly (replace the example IDs with IDs from your catalog):

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:gb="GreenBox.I18n.Unity.UIElements">
    <ui:Label name="title">
        <Bindings>
            <gb:I18nTextBinding property="text" entry-id="1234567890123456789" />
        </Bindings>
    </ui:Label>
    <ui:VisualElement name="icon">
        <Bindings>
            <gb:I18nSpriteBinding property="style.backgroundImage" entry-id="1234567890123456789" />
        </Bindings>
    </ui:VisualElement>
</ui:UXML>
```

Bindings can also be added from code after the document has loaded:

```csharp
using GreenBox.I18n.Unity.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class LocalizedMenu : MonoBehaviour
{
    [SerializeField] private UIDocument _document;
    [SerializeField] private I18nKey _titleKey;

    private void OnEnable()
    {
        Label title = _document.rootVisualElement.Q<Label>("title");
        title.SetBinding("text", new I18nTextBinding { EntryId = _titleKey.Id });
    }
}
```

For dynamic text with formatting arguments, call `I18n.Text(key, ...)` from the UI controller and refresh it on `I18n.LocaleChanged`. The declarative text binding currently handles entries without arguments.
