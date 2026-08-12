using System.Reflection;

namespace GreenBox.I18n.Desktop;

internal static class ProductVersion
{
    internal static string Current { get; } = ReadCurrentVersion();

    internal static string UpdateFeedUrl { get; } =
        Environment.GetEnvironmentVariable("GREENBOX_I18N_UPDATE_URL")
        ?? ReadMetadata("GreenBoxUpdateFeedUrl")
        ?? string.Empty;

    private static string ReadCurrentVersion()
    {
        string? version = typeof(ProductVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (string.IsNullOrWhiteSpace(version))
        {
            return "development";
        }

        int metadataIndex = version.IndexOf('+', StringComparison.Ordinal);
        return metadataIndex >= 0 ? version[..metadataIndex] : version;
    }

    private static string? ReadMetadata(string key) => typeof(ProductVersion).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))?
        .Value;
}
