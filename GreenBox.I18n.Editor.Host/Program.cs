using GreenBox.I18n.Editor.Host.Editor;
using GreenBox.I18n.Editor.Host.Endpoints;
using GreenBox.I18n.Editor.Host.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<EditorSession>();
builder.Services.AddSingleton<CatalogFileLoader>();
builder.Services.AddSingleton<UnityAssetReferenceService>();

var app = builder.Build();

app.MapEditorSessionEndpoints();
app.MapCatalogEndpoints();
app.MapUnityAssetEndpoints();

app.Run();
