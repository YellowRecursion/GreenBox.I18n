using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

const string cldrJsonRelease = "48.2.0";
const string cldrDisplayVersion = "48.2";
string cardinalPath;
string ordinalPath;
string outputPath;
string conformanceOutputPath;

if (args.Length == 0)
{
    string sourceDirectory = Path.Combine(Path.GetTempPath(), "greenbox-i18n-cldr", cldrJsonRelease);
    Directory.CreateDirectory(sourceDirectory);
    cardinalPath = Path.Combine(sourceDirectory, "plurals.json");
    ordinalPath = Path.Combine(sourceDirectory, "ordinals.json");
    outputPath = Path.Combine(
        Directory.GetCurrentDirectory(),
        "GreenBox.I18n.Core",
        "Messages",
        "Generated",
        "I18nPluralData.Generated.cs");
    conformanceOutputPath = Path.Combine(
        Directory.GetCurrentDirectory(),
        "GreenBox.I18n.Core.Tests",
        "Messages",
        "Generated",
        "I18nCldrConformanceCases.Generated.cs");

    using var http = new HttpClient();
    await Download(
        http,
        $"https://raw.githubusercontent.com/unicode-org/cldr-json/{cldrJsonRelease}/cldr-json/cldr-core/supplemental/plurals.json",
        cardinalPath);
    await Download(
        http,
        $"https://raw.githubusercontent.com/unicode-org/cldr-json/{cldrJsonRelease}/cldr-json/cldr-core/supplemental/ordinals.json",
        ordinalPath);
}
else if (args.Length == 4)
{
    cardinalPath = args[0];
    ordinalPath = args[1];
    outputPath = args[2];
    conformanceOutputPath = args[3];
}
else
{
    Console.Error.WriteLine(
        "Usage: GreenBox.I18n.CldrGenerator [<plurals.json> <ordinals.json> <runtime-output.cs> <conformance-output.cs>]");
    return 1;
}

PluralDocument cardinal = PluralDocument.Load(cardinalPath, "plurals-type-cardinal");
PluralDocument ordinal = PluralDocument.Load(ordinalPath, "plurals-type-ordinal");
GeneratedData data = GeneratedData.Build(cardinal, ordinal);
string output = SourceWriter.Write(data, cldrDisplayVersion);
string conformanceOutput = ConformanceSourceWriter.Write(cardinal, ordinal, cldrDisplayVersion);

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
WriteUtf8WithLf(outputPath, output);
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(conformanceOutputPath))!);
WriteUtf8WithLf(conformanceOutputPath, conformanceOutput);
Console.WriteLine(
    $"Generated CLDR {cldrDisplayVersion}: {data.Locales.Count} locales, " +
    $"{data.RuleSets.Count} unique rule sets, {data.Relations.Count} relations.");
return 0;

static void WriteUtf8WithLf(string path, string content)
{
    File.WriteAllText(path, content.Replace("\r\n", "\n"), new UTF8Encoding(false));
}

static async Task Download(HttpClient http, string url, string path)
{
    byte[] content = await http.GetByteArrayAsync(url);
    await File.WriteAllBytesAsync(path, content);
}

internal sealed record PluralDocument(string CldrVersion, Dictionary<string, LocaleRules> Locales)
{
    public static PluralDocument Load(string path, string sectionName)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement supplemental = document.RootElement.GetProperty("supplemental");
        string version = supplemental.GetProperty("version").GetProperty("_cldrVersion").GetString()!;
        var locales = new Dictionary<string, LocaleRules>(StringComparer.Ordinal);

        foreach (JsonProperty locale in supplemental.GetProperty(sectionName).EnumerateObject())
        {
            var rules = new List<CategoryCondition>();
            foreach (JsonProperty property in locale.Value.EnumerateObject())
            {
                string category = property.Name["pluralRule-count-".Length..];
                string source = property.Value.GetString()!;
                string condition = source.Split('@')[0].Trim();
                rules.Add(new CategoryCondition(category, condition, SampleParser.Parse(source)));
            }

            locales.Add(locale.Name.Replace('_', '-'), new LocaleRules(rules));
        }

        return new PluralDocument(version, locales);
    }
}

internal sealed record LocaleRules(List<CategoryCondition> Categories)
{
    public string Identity => string.Join(
        "|",
        Categories
            .Where(category => category.Category != "other")
            .Select(category => category.Category + ":" + category.Condition));
}

internal sealed record CategoryCondition(string Category, string Condition, IReadOnlyList<string> Samples);

internal sealed class GeneratedData
{
    public List<LocaleMap> Locales { get; } = new();
    public List<RuleSetData> RuleSets { get; } = new();
    public List<CategoryRuleData> Categories { get; } = new();
    public List<AndGroupData> Groups { get; } = new();
    public List<RelationData> Relations { get; } = new();
    public List<RangeData> Ranges { get; } = new();

    public static GeneratedData Build(PluralDocument cardinal, PluralDocument ordinal)
    {
        var result = new GeneratedData();
        var ruleSetIds = new Dictionary<string, ushort>(StringComparer.Ordinal);
        result.AddRuleSet(new LocaleRules(new List<CategoryCondition>()), ruleSetIds);

        string[] localeNames = cardinal.Locales.Keys
            .Concat(ordinal.Locales.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(locale => locale, StringComparer.Ordinal)
            .ToArray();

        foreach (string locale in localeNames)
        {
            ushort cardinalId = result.AddRuleSet(
                cardinal.Locales.GetValueOrDefault(locale) ?? new LocaleRules(new List<CategoryCondition>()),
                ruleSetIds);
            ushort ordinalId = result.AddRuleSet(
                ordinal.Locales.GetValueOrDefault(locale) ?? new LocaleRules(new List<CategoryCondition>()),
                ruleSetIds);
            result.Locales.Add(new LocaleMap(locale, cardinalId, ordinalId));
        }

        return result;
    }

    private ushort AddRuleSet(LocaleRules rules, Dictionary<string, ushort> ids)
    {
        if (ids.TryGetValue(rules.Identity, out ushort existing))
        {
            return existing;
        }

        ushort id = checked((ushort)RuleSets.Count);
        ids.Add(rules.Identity, id);
        int categoryOffset = Categories.Count;

        List<CategoryCondition> runtimeCategories = rules.Categories
            .Where(category => category.Category != "other")
            .ToList();
        foreach (CategoryCondition category in runtimeCategories)
        {
            List<List<ParsedRelation>> groups = RuleParser.Parse(category.Condition);
            int groupOffset = Groups.Count;
            foreach (List<ParsedRelation> group in groups)
            {
                int relationOffset = Relations.Count;
                foreach (ParsedRelation relation in group)
                {
                    int rangeOffset = Ranges.Count;
                    Ranges.AddRange(relation.Ranges.Select(range => new RangeData(range.Start, range.End)));
                    Relations.Add(
                        new RelationData(
                            relation.Operand,
                            relation.Modulo,
                            relation.Negated,
                            rangeOffset,
                            relation.Ranges.Count));
                }

                Groups.Add(new AndGroupData(relationOffset, group.Count));
            }

            Categories.Add(
                new CategoryRuleData(
                    category.Category,
                    groupOffset,
                    groups.Count));
        }

        RuleSets.Add(new RuleSetData(categoryOffset, runtimeCategories.Count));
        return id;
    }
}

internal static partial class SampleParser
{
    private static readonly Regex SampleGroup = new(
        "@(integer|decimal)\\s+([^@]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<string> Parse(string rule)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in SampleGroup.Matches(rule))
        {
            foreach (string rawItem in match.Groups[2].Value.Split(','))
            {
                string item = rawItem.Trim();
                if (item.Length == 0 || item.Contains('…'))
                {
                    continue;
                }

                AddItem(result, item);
            }
        }

        return result.ToArray();
    }

    private static void AddItem(HashSet<string> result, string item)
    {
        string[] bounds = item.Split('~');
        if (bounds.Length == 1)
        {
            result.Add(item);
            return;
        }

        if (bounds.Length != 2 || bounds[0].Contains('c') || bounds[1].Contains('c'))
        {
            result.Add(bounds[0]);
            result.Add(bounds[^1]);
            return;
        }

        int scale = Math.Max(Scale(bounds[0]), Scale(bounds[1]));
        decimal step = 1m / Pow10(scale);
        decimal start = decimal.Parse(bounds[0], CultureInfo.InvariantCulture);
        decimal end = decimal.Parse(bounds[1], CultureInfo.InvariantCulture);
        for (decimal value = start; value <= end; value += step)
        {
            result.Add(value.ToString($"F{scale}", CultureInfo.InvariantCulture));
        }
    }

    private static int Scale(string value)
    {
        int point = value.IndexOf('.');
        return point < 0 ? 0 : value.Length - point - 1;
    }

    private static decimal Pow10(int exponent)
    {
        decimal result = 1m;
        for (int i = 0; i < exponent; i++)
        {
            result *= 10m;
        }

        return result;
    }
}

internal static class RuleParser
{
    public static List<List<ParsedRelation>> Parse(string condition)
    {
        return Split(condition, " or ")
            .Select(andGroup => Split(andGroup, " and ").Select(ParseRelation).ToList())
            .ToList();
    }

    private static ParsedRelation ParseRelation(string source)
    {
        string[] tokens = source.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 3)
        {
            throw new InvalidDataException($"Invalid CLDR plural relation: {source}");
        }

        string operand = tokens[0];
        int index = 1;
        int modulo = 0;
        if (tokens[index] == "%")
        {
            modulo = int.Parse(tokens[++index], CultureInfo.InvariantCulture);
            index++;
        }

        bool negated = tokens[index++] == "!=";
        var ranges = new List<ParsedRange>();
        foreach (string item in string.Concat(tokens[index..]).Split(','))
        {
            string[] bounds = item.Split("..", StringSplitOptions.None);
            decimal start = decimal.Parse(bounds[0], NumberStyles.Float, CultureInfo.InvariantCulture);
            decimal end = bounds.Length == 1
                ? start
                : decimal.Parse(bounds[1], NumberStyles.Float, CultureInfo.InvariantCulture);
            ranges.Add(new ParsedRange(start, end));
        }

        return new ParsedRelation(operand, modulo, negated, ranges);
    }

    private static IEnumerable<string> Split(string source, string separator)
    {
        return source.Split(separator, StringSplitOptions.None).Select(value => value.Trim());
    }
}

internal static class SourceWriter
{
    public static string Write(GeneratedData data, string cldrVersion)
    {
        var output = new StringBuilder();
        output.AppendLine("// <auto-generated />");
        output.AppendLine($"// Unicode CLDR {cldrVersion}. Generated by GreenBox.I18n.CldrGenerator.");
        output.AppendLine("namespace GreenBox.I18n");
        output.AppendLine("{");
        output.AppendLine("    internal static partial class I18nPluralData");
        output.AppendLine("    {");
        output.AppendLine($"        public const string CldrVersion = \"{cldrVersion}\";");
        WriteArray(output, "LocaleMap", "Locales", data.Locales.Select(Format));
        WriteArray(output, "RuleSet", "RuleSets", data.RuleSets.Select(Format));
        WriteArray(output, "CategoryRule", "Categories", data.Categories.Select(Format));
        WriteArray(output, "AndGroup", "Groups", data.Groups.Select(Format));
        WriteArray(output, "Relation", "Relations", data.Relations.Select(Format));
        WriteArray(output, "Range", "Ranges", data.Ranges.Select(Format));
        output.AppendLine("    }");
        output.AppendLine("}");
        return output.ToString();
    }

    private static void WriteArray(
        StringBuilder output,
        string type,
        string name,
        IEnumerable<string> values)
    {
        output.AppendLine();
        output.AppendLine($"        public static readonly {type}[] {name} =");
        output.AppendLine("        {");
        foreach (string value in values)
        {
            output.Append("            ").Append(value).AppendLine(",");
        }

        output.AppendLine("        };");
    }

    private static string Format(LocaleMap value) =>
        $"new LocaleMap(\"{value.Locale}\", {value.CardinalRuleSet}, {value.OrdinalRuleSet})";

    private static string Format(RuleSetData value) =>
        $"new RuleSet({value.CategoryOffset}, {value.CategoryCount})";

    private static string Format(CategoryRuleData value) =>
        $"new CategoryRule(PluralCategory.{Title(value.Category)}, {value.GroupOffset}, {value.GroupCount})";

    private static string Format(AndGroupData value) =>
        $"new AndGroup({value.RelationOffset}, {value.RelationCount})";

    private static string Format(RelationData value) =>
        $"new Relation(PluralOperand.{value.Operand.ToUpperInvariant()}, {value.Modulo}, " +
        $"{value.Negated.ToString().ToLowerInvariant()}, {value.RangeOffset}, {value.RangeCount})";

    private static string Format(RangeData value) =>
        $"new Range({Decimal(value.Start)}, {Decimal(value.End)})";

    private static string Decimal(decimal value) =>
        value.ToString(CultureInfo.InvariantCulture) + "m";

    private static string Title(string value) =>
        char.ToUpperInvariant(value[0]) + value[1..];
}

internal static class ConformanceSourceWriter
{
    public static string Write(PluralDocument cardinal, PluralDocument ordinal, string cldrVersion)
    {
        var output = new StringBuilder();
        output.AppendLine("// <auto-generated />");
        output.AppendLine($"// Official Unicode CLDR {cldrVersion} plural-rule examples.");
        output.AppendLine("namespace GreenBox.I18n.Core.Tests;");
        output.AppendLine();
        output.AppendLine("internal static class I18nCldrConformanceCases");
        output.AppendLine("{");
        output.AppendLine("    public static readonly Case[] All =");
        output.AppendLine("    {");
        WriteDocument(output, cardinal, ordinal: false);
        WriteDocument(output, ordinal, ordinal: true);
        output.AppendLine("    };");
        output.AppendLine();
        output.AppendLine("    internal readonly record struct Case(string Locale, bool Ordinal, string Sample, string Expected);");
        output.AppendLine("}");
        return output.ToString();
    }

    private static void WriteDocument(StringBuilder output, PluralDocument document, bool ordinal)
    {
        foreach ((string locale, LocaleRules rules) in document.Locales.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            foreach (CategoryCondition category in rules.Categories)
            {
                foreach (string sample in category.Samples)
                {
                    output.Append("        new Case(\"")
                        .Append(locale)
                        .Append("\", ")
                        .Append(ordinal ? "true" : "false")
                        .Append(", \"")
                        .Append(sample)
                        .Append("\", \"")
                        .Append(category.Category)
                        .AppendLine("\"),");
                }
            }
        }
    }
}

internal sealed record LocaleMap(string Locale, ushort CardinalRuleSet, ushort OrdinalRuleSet);
internal sealed record RuleSetData(int CategoryOffset, int CategoryCount);
internal sealed record CategoryRuleData(string Category, int GroupOffset, int GroupCount);
internal sealed record AndGroupData(int RelationOffset, int RelationCount);
internal sealed record RelationData(string Operand, int Modulo, bool Negated, int RangeOffset, int RangeCount);
internal sealed record RangeData(decimal Start, decimal End);
internal sealed record ParsedRelation(string Operand, int Modulo, bool Negated, List<ParsedRange> Ranges);
internal sealed record ParsedRange(decimal Start, decimal End);
