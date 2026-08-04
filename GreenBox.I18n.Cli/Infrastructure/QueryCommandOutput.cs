using GreenBox.I18n;

namespace GreenBox.I18n.Cli;

internal static class QueryCommandOutput
{
    public static int WriteLoadError(
        FileInfo catalogFile,
        CatalogLoadResult result,
        bool writeJson,
        TextWriter standardOutput,
        TextWriter standardError)
    {
        CliError error = result.Error!;
        if (writeJson)
        {
            standardOutput.WriteLine(CliJson.Serialize(new CliErrorReport
            {
                File = catalogFile.FullName,
                Error = error,
            }));
        }
        else
        {
            standardError.WriteLine($"ERROR [{error.Code}] {error.Message}");
        }

        return result.ExitCode;
    }

    public static void WriteEntry(I18nEntry entry, TextWriter writer)
    {
        writer.WriteLine($"{entry.Id}  {entry.Path}");

        if (entry.Comment != null)
        {
            writer.WriteLine($"  Comment: {entry.Comment}");
        }

        foreach ((string locale, I18nLocaleValue value) in entry.Locales.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            string text = value.Text == null ? string.Empty : $"text: {value.Text}";
            string asset = value.Asset == null
                ? string.Empty
                : $"asset: {value.Asset.AssetGuid}" +
                  (value.Asset.LocalFileId == null ? string.Empty : $"#{value.Asset.LocalFileId}");
            string separator = text.Length > 0 && asset.Length > 0 ? "; " : string.Empty;
            writer.WriteLine($"  {locale}: {text}{separator}{asset}");
        }
    }
}
