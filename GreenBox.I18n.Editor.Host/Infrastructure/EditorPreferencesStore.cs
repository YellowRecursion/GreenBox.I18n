using System.Text.Json;
using GreenBox.I18n.Editor.Host.Contracts;

namespace GreenBox.I18n.Editor.Host.Infrastructure;

/// <summary>
/// Persists personal editor preferences for the current operating-system user.
/// </summary>
public sealed class EditorPreferencesStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly object _sync = new();
    private readonly string _filePath;
    private EditorPreferencesData _preferences;
    private string? _restoreError;

    /// <summary>
    /// Creates the store in the current user's local application-data directory.
    /// </summary>
    public EditorPreferencesStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GreenBox",
            "I18n",
            "preferences.json"))
    {
    }

    /// <summary>
    /// Creates the store at an explicit path.
    /// </summary>
    /// <param name="filePath">The preferences file path.</param>
    public EditorPreferencesStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = Path.GetFullPath(filePath);
        _preferences = Load(_filePath);
    }

    /// <summary>
    /// Returns the current preferences snapshot.
    /// </summary>
    public EditorPreferencesResponse GetSnapshot()
    {
        lock (_sync)
        {
            return CreateResponse();
        }
    }

    /// <summary>
    /// Updates personal editor preferences as one persisted snapshot.
    /// </summary>
    public EditorPreferencesResponse Update(UpdateEditorPreferencesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_sync)
        {
            EditorPreferencesData updated = _preferences with
            {
                ReopenLastCatalog = request.ReopenLastCatalog,
                WarnUnusedEntries = request.WarnUnusedEntries,
                WarnIncompleteEntries = request.WarnIncompleteEntries,
            };
            Save(updated);
            _preferences = updated;
            return CreateResponse();
        }
    }

    /// <summary>
    /// Records a catalog after it has been opened successfully.
    /// </summary>
    public void RecordLastCatalog(string catalogPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogPath);
        lock (_sync)
        {
            EditorPreferencesData updated = _preferences with
            {
                LastCatalogPath = Path.GetFullPath(catalogPath),
            };
            Save(updated);
            _preferences = updated;
            _restoreError = null;
        }
    }

    /// <summary>
    /// Records a non-fatal startup restore error.
    /// </summary>
    public void SetRestoreError(string? message)
    {
        lock (_sync)
        {
            _restoreError = message;
        }
    }

    private EditorPreferencesResponse CreateResponse()
    {
        return new EditorPreferencesResponse(
            _preferences.ReopenLastCatalog,
            _preferences.WarnUnusedEntries,
            _preferences.WarnIncompleteEntries,
            _preferences.LastCatalogPath,
            _restoreError);
    }

    private static EditorPreferencesData Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new EditorPreferencesData();
        }

        try
        {
            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<EditorPreferencesData>(json) ?? new EditorPreferencesData();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new EditorPreferencesData();
        }
    }

    private void Save(EditorPreferencesData preferences)
    {
        string directory = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(_filePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(preferences, SerializerOptions));
            File.Move(temporaryPath, _filePath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private sealed record EditorPreferencesData
    {
        public bool ReopenLastCatalog { get; init; } = true;

        public bool WarnUnusedEntries { get; init; } = true;

        public bool WarnIncompleteEntries { get; init; }

        public string? LastCatalogPath { get; init; }
    }
}
