using GreenBox.I18n;

namespace GreenBox.I18n.Core.Tests;

public sealed class I18nCompiledCatalogTests
{
    [Fact]
    public void BinaryRoundTrip_PreservesRuntimeTextFallbackAndAssets()
    {
        I18nCatalog source = CreateCatalog();
        I18nCompiledCatalogCompilation compilation = I18nCompiledCatalogCompiler.Compile(source);

        byte[] data = I18nCompiledCatalogBinary.Serialize(compilation.Catalog!);
        var runtime = new I18nRuntime(I18nCompiledCatalogBinary.Deserialize(data), "ru");

        Assert.True(compilation.IsSuccess);
        Assert.Equal("Play", runtime.Text(3857333080842830204));
        Assert.Equal("0123456789abcdef0123456789abcdef", runtime.Asset(3857333080842830204)!.AssetGuid);
        Assert.Equal("ru-RU", runtime.CurrentCulture.Name);
    }

    [Fact]
    public void BinaryRoundTrip_PreservesCompiledMessageProgram()
    {
        I18nCatalog source = CreateCatalog();
        source.Entries[0].Locales["en"].Text =
            ".input {$count :number}\n.match $count\none {{One item}}\n* {{Items}}";
        source.Entries[0].Locales["ru"].Text = null;

        I18nCompiledCatalogCompilation compilation = I18nCompiledCatalogCompiler.Compile(source);
        byte[] data = I18nCompiledCatalogBinary.Serialize(compilation.Catalog!);
        byte[] secondData = I18nCompiledCatalogBinary.Serialize(compilation.Catalog!);
        var runtime = new I18nRuntime(I18nCompiledCatalogBinary.Deserialize(data));

        Assert.Equal(data, secondData);
        Assert.Equal("One item", runtime.Text(3857333080842830204, ("count", 1)));
        Assert.Equal("Items", runtime.Text(3857333080842830204, ("count", 2)));
    }

    [Fact]
    public void Compile_DifferentArgumentSetsAcrossPopulatedLocales_Fails()
    {
        I18nCatalog source = CreateCatalog();
        source.Entries[0].Locales["en"].Text = "Hello {$name}";
        source.Entries[0].Locales["ru"].Text = "Привет {$player}";

        I18nCompiledCatalogCompilation result = I18nCompiledCatalogCompiler.Compile(source);

        Assert.False(result.IsSuccess);
        Assert.Single(result.Diagnostics);
        Assert.Equal("inconsistent_arguments", result.Diagnostics[0].Diagnostic.Code);
    }

    [Fact]
    public void Compile_DifferentArgumentTypesAcrossPopulatedLocales_Fails()
    {
        I18nCatalog source = CreateCatalog();
        source.Entries[0].Locales["en"].Text =
            ".input {$value :number}\n{{{$value}}}";
        source.Entries[0].Locales["ru"].Text =
            ".input {$value :string}\n{{{$value}}}";

        I18nCompiledCatalogCompilation result = I18nCompiledCatalogCompiler.Compile(source);

        Assert.False(result.IsSuccess);
        Assert.Equal("inconsistent_arguments", Assert.Single(result.Diagnostics).Diagnostic.Code);
    }

    [Fact]
    public void BinaryRoundTrip_PreservesMultipleSelectorsAndNumberOptions()
    {
        I18nCatalog source = CreateCatalog();
        source.Entries[0].Locales["en"].Text =
            ".input {$gender :string}\n" +
            ".input {$count :number minimumFractionDigits=2 maximumFractionDigits=2}\n" +
            ".match $gender $count\n" +
            "male one {{One}}\n" +
            "male * {{He has {$count}}}\n" +
            "* * {{They have {$count}}}";
        source.Entries[0].Locales["ru"].Text = null;

        I18nCompiledCatalog compiled = I18nCompiledCatalogCompiler.Compile(source).Catalog!;
        var runtime = new I18nRuntime(
            I18nCompiledCatalogBinary.Deserialize(I18nCompiledCatalogBinary.Serialize(compiled)));

        Assert.Equal(
            "He has 2.00",
            runtime.Text(3857333080842830204, ("gender", "male"), ("count", 2)));
    }

    [Fact]
    public void BinaryRuntime_EightArguments_UsesTypedOverload()
    {
        I18nCatalog source = CreateCatalog();
        source.Entries[0].Locales["en"].Text =
            "{$a}{$b}{$c}{$d}{$e}{$f}{$g}{$h}";
        source.Entries[0].Locales["ru"].Text = null;
        I18nCompiledCatalog compiled = I18nCompiledCatalogCompiler.Compile(source).Catalog!;
        var runtime = new I18nRuntime(
            I18nCompiledCatalogBinary.Deserialize(I18nCompiledCatalogBinary.Serialize(compiled)));

        string result = runtime.Text(
            3857333080842830204,
            ("a", 1), ("b", 2), ("c", 3), ("d", 4),
            ("e", 5), ("f", 6), ("g", 7), ("h", 8));

        Assert.Equal("12345678", result);
    }

    [Fact]
    public void Compile_UnpopulatedLocale_DoesNotParticipateInArgumentValidation()
    {
        I18nCatalog source = CreateCatalog();
        source.Entries[0].Locales["en"].Text = "Hello {$name}";
        source.Entries[0].Locales["ru"].Text = null;

        I18nCompiledCatalogCompilation result = I18nCompiledCatalogCompiler.Compile(source);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Deserialize_UnsupportedVersion_RequiresRegeneration()
    {
        I18nCompiledCatalog compiled = I18nCompiledCatalogCompiler.Compile(CreateCatalog()).Catalog!;
        byte[] data = I18nCompiledCatalogBinary.Serialize(compiled);
        data[4] = 99;

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => I18nCompiledCatalogBinary.Deserialize(data));

        Assert.Contains("Regenerate", exception.Message);
    }

    [Fact]
    public void BinaryRoundTrip_TenThousandEntries_RemainsUsable()
    {
        I18nCatalog source = CreateCatalog();
        source.Entries.Clear();
        for (ulong index = 0; index < 10_000; index++)
        {
            source.Entries.Add(new I18nEntry
            {
                Id = I18nEntryId.Create(index).ToString(),
                Path = "Generated.Entry" + index,
                Locales =
                {
                    ["en"] = new I18nLocaleValue { Text = "Value " + index },
                    ["ru"] = new I18nLocaleValue(),
                },
            });
        }

        I18nCompiledCatalog compiled = I18nCompiledCatalogCompiler.Compile(source).Catalog!;
        byte[] data = I18nCompiledCatalogBinary.Serialize(compiled);
        long deserializeAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        I18nCompiledCatalog loaded = I18nCompiledCatalogBinary.Deserialize(data);
        long deserializeAllocation = GC.GetAllocatedBytesForCurrentThread() - deserializeAllocatedBefore;

        _ = new I18nRuntime(I18nCompiledCatalogCompiler.Compile(CreateCatalog()).Catalog!);
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var runtime = new I18nRuntime(loaded);
        long runtimeAllocation = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.Equal("Value 9999", runtime.Text(I18nEntryId.Create(9999)));
        Assert.True(data.Length < 2_000_000);
        Assert.True(
            deserializeAllocation < 20_000_000,
            $"Deserialization allocated {deserializeAllocation:N0} bytes for a 10,000-entry catalog.");
        Assert.True(
            runtimeAllocation < 250_000,
            $"Runtime construction allocated {runtimeAllocation:N0} bytes for a 10,000-entry catalog.");
        Assert.Equal(10_000, loaded.Storage.Entries.Length);
        Assert.Equal(20_000, loaded.Storage.Values.Length);
        Assert.Equal(10_000, loaded.Storage.Messages.Length);
        Assert.Equal(10_000, loaded.Storage.MessageParts.Length);
    }

    [Fact]
    public void Storage_InvalidMessageReference_IsRejectedAtLoadBoundary()
    {
        I18nCompiledCatalogStorage source =
            I18nCompiledCatalogCompiler.Compile(CreateCatalog()).Catalog!.Storage;
        I18nCompiledValueRecord[] values = source.Values.ToArray();
        values[0] = new I18nCompiledValueRecord(values[0].Locale, 999, values[0].Asset);

        Assert.Throws<InvalidDataException>(() => new I18nCompiledCatalogStorage(
            source.DefaultLocale,
            source.Strings,
            source.Locales,
            source.FallbackLocales,
            source.Entries,
            values,
            source.Messages,
            source.MessageArguments,
            source.MessageParts,
            source.Selectors,
            source.Variants,
            source.VariantKeys));
    }

    private static I18nCatalog CreateCatalog()
    {
        return new I18nCatalog
        {
            DefaultLocale = "en",
            Locales =
            {
                new I18nLocaleDefinition
                {
                    Id = "en", DisplayName = "English", Culture = "en-US",
                },
                new I18nLocaleDefinition
                {
                    Id = "ru", DisplayName = "Русский", Culture = "ru-RU", Fallback = "en",
                },
            },
            Entries =
            {
                new I18nEntry
                {
                    Id = "3857333080842830204",
                    Path = "Menu.Play",
                    Locales =
                    {
                        ["en"] = new I18nLocaleValue
                        {
                            Text = "Play",
                            Asset = new I18nAssetReference
                            {
                                AssetGuid = "0123456789abcdef0123456789abcdef",
                                LocalFileId = "42",
                            },
                        },
                        ["ru"] = new I18nLocaleValue(),
                    },
                },
            },
        };
    }
}
