namespace GreenBox.I18n.Cli.Tests;

internal sealed class TestCatalog : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        "GreenBox.I18n.Tests",
        Guid.NewGuid().ToString("N"));

    public TestCatalog()
    {
        Directory.CreateDirectory(_temporaryDirectory);
    }

    public FileInfo Write(string json)
    {
        string path = Path.Combine(_temporaryDirectory, "catalog.json");
        File.WriteAllText(path, json);
        return new FileInfo(path);
    }

    public FileInfo MissingFile => new(Path.Combine(_temporaryDirectory, "missing.json"));

    public void Dispose()
    {
        Directory.Delete(_temporaryDirectory, true);
    }

    public const string ValidJson =
        """
        {
          "schemaVersion": 1,
          "defaultLocale": "en",
          "locales": [
            {
              "id": "en",
              "displayName": "English",
              "culture": "en-US"
            }
          ],
          "entries": [
            {
              "id": "10",
              "path": "Menu.History",
              "locales": {
                "en": {
                  "text": "Recent reports"
                }
              }
            },
            {
              "id": "20",
              "path": "Reports.Title",
              "comment": "Report heading",
              "locales": {
                "en": {
                  "text": "Reports"
                }
              }
            }
          ]
        }
        """;
}
