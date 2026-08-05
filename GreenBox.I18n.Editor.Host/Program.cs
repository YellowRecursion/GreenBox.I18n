using GreenBox.I18n.Editor.Host.Editor;
using GreenBox.I18n.Editor.Host.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<EditorSession>();

var app = builder.Build();

app.MapEditorSessionEndpoints();

app.Run();
