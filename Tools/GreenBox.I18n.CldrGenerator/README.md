# CLDR data generator

GreenBox I18n ships compact plural-rule tables generated from a pinned Unicode CLDR release.
Game projects never run this tool and do not need the CLDR JSON files.

From the repository root, update the generated tables with:

```shell
dotnet run --project Tools/GreenBox.I18n.CldrGenerator
```

The release is pinned in `Program.cs`. Updating it is an intentional repository maintenance task:
change the version, regenerate the file, review the diff, and run `GreenBox.I18n.Core.Tests`.

The generator writes two files:

- compact runtime data to
  `GreenBox.I18n.Core/Messages/Generated/I18nPluralData.Generated.cs`;
- official CLDR conformance examples to
  `GreenBox.I18n.Core.Tests/Messages/Generated/I18nCldrConformanceCases.Generated.cs`.

The conformance data is test-only and is not shipped in the game runtime.
