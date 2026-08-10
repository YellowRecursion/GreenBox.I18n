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
        return Write("catalog.json", json);
    }

    public FileInfo Write(string fileName, string json)
    {
        string path = Path.Combine(_temporaryDirectory, fileName);
        File.WriteAllText(path, json);
        return new FileInfo(path);
    }

    public FileInfo GetFile(string fileName)
    {
        return new FileInfo(Path.Combine(_temporaryDirectory, fileName));
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
              "id": "3857333080842832461",
              "path": "Menu.History",
              "locales": {
                "en": {
                  "text": "Recent reports"
                }
              }
            },
            {
              "id": "3857333080842834967",
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
