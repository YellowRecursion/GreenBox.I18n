using GreenBox.I18n.Editor.Host.Contracts;
using GreenBox.I18n.Editor.Host.Editor;
using GreenBox.I18n.Editor.Host.Endpoints;
using GreenBox.I18n.Editor.Host.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<EditorSession>();
builder.Services.AddSingleton<CatalogFileLoader>();
builder.Services.AddSingleton<CatalogFilePicker>();
builder.Services.AddSingleton<UnityProjectLocator>();
builder.Services.AddSingleton<UnityProjectPresenceService>();
builder.Services.AddSingleton<UnityAssetReferenceService>();
builder.Services.AddSingleton<UsageIndexReader>();
builder.Services.AddSingleton<UnityEditorCommandTransport>();
builder.Services.AddSingleton<UsageNavigationService>();
builder.Services.AddSingleton<EditorPreferencesStore>();

var app = builder.Build();

var preferences = app.Services.GetRequiredService<EditorPreferencesStore>();
EditorPreferencesResponse preferenceSnapshot = preferences.GetSnapshot();
if (preferenceSnapshot.ReopenLastCatalog &&
    !string.IsNullOrWhiteSpace(preferenceSnapshot.LastCatalogPath))
{
    var loader = app.Services.GetRequiredService<CatalogFileLoader>();
    CatalogLoadResult loadResult = await loader.LoadAsync(
        preferenceSnapshot.LastCatalogPath,
        CancellationToken.None);
    if (loadResult.IsSuccess)
    {
        app.Services.GetRequiredService<EditorSession>().Open(
            loadResult.CatalogPath!,
            loadResult.Catalog!,
            loadResult.ContentHash!);
    }
    else
    {
        preferences.SetRestoreError(loadResult.Error!.Message);
    }
}

app.MapEditorSessionEndpoints();
app.MapCatalogEndpoints();
app.MapUnityAssetEndpoints();
app.MapUnityProjectEndpoints();
app.MapUsageIndexEndpoints();
app.MapEditorPreferencesEndpoints();

app.Run();
