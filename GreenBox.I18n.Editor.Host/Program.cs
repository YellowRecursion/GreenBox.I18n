using GreenBox.I18n.Editor.Host.Editor;
using GreenBox.I18n.Editor.Host.Endpoints;
using GreenBox.I18n.Editor.Host.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<EditorSession>();
builder.Services.AddSingleton<CatalogFileLoader>();

var app = builder.Build();

app.MapEditorSessionEndpoints();

app.Run();
