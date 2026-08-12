using GreenBox.I18n.Editor.Host.Contracts;
using GreenBox.I18n.Editor.Host.Endpoints;
using GreenBox.I18n.Editor.Host.Infrastructure;
using GreenBox.I18n.Workspace;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace GreenBox.I18n.Editor.Host;

public static class EditorHostApplication
{
    public static async Task RunAsync(string[] args)
    {
        WebApplication app = Build(args);
        await app.RunAsync();
    }

    public static WebApplication Build(
        string[] args,
        Action<string>? logSink = null,
        bool enableConsoleLogging = true)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        if (!enableConsoleLogging)
        {
            builder.Logging.ClearProviders();
        }

        if (logSink is not null)
        {
            builder.Logging.AddProvider(new CallbackLoggerProvider(logSink));
        }

        builder.Services.AddSingleton<CatalogWorkspace>();
        builder.Services.AddSingleton<CatalogFileLoader>();
        builder.Services.AddSingleton<CatalogFilePicker>();
        builder.Services.AddSingleton<UnityProjectLocator>();
        builder.Services.AddSingleton<UnityProjectPresenceService>();
        builder.Services.AddSingleton<UnityAssetReferenceService>();
        builder.Services.AddSingleton<UsageIndexReader>();
        builder.Services.AddSingleton<UnityEditorCommandTransport>();
        builder.Services.AddSingleton<UsageNavigationService>();
        builder.Services.AddSingleton<EditorPreferencesStore>();
        builder.Services.AddSingleton<CodexMcpIntegrationService>();
        builder.Services.AddHostedService<CatalogRestoreHostedService>();

        WebApplication app = builder.Build();
        MapApplication(app);
        return app;
    }

    private static void MapApplication(WebApplication app)
    {
        string webRootPath = app.Environment.WebRootPath ??
                             Path.Combine(app.Environment.ContentRootPath, "wwwroot");
        string indexPath = Path.Combine(webRootPath, "index.html");
        bool hasEditorWeb = File.Exists(indexPath);
        if (hasEditorWeb)
        {
            app.UseDefaultFiles();
            app.UseStaticFiles();
        }

        app.MapSessionEndpoints();
        app.MapCatalogEndpoints();
        app.MapMessageEndpoints();
        app.MapUnityAssetEndpoints();
        app.MapUnityProjectEndpoints();
        app.MapUsageIndexEndpoints();
        app.MapEditorPreferencesEndpoints();
        app.MapIntegrationEndpoints();
        app.MapWorkspaceEndpoints();

        app.MapGet("/api/health", () => Results.Ok(new
        {
            product = "GreenBox.I18n",
            status = "ready",
            version = GetProductVersion(),
        }));

        if (hasEditorWeb)
        {
            app.MapFallbackToFile("index.html");
        }
    }

    private static string GetProductVersion()
    {
        string? version = typeof(EditorHostApplication).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (string.IsNullOrWhiteSpace(version))
        {
            return "development";
        }

        int metadataIndex = version.IndexOf('+', StringComparison.Ordinal);
        return metadataIndex >= 0 ? version[..metadataIndex] : version;
    }

    private sealed class CallbackLoggerProvider(Action<string> sink) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CallbackLogger(categoryName, sink);

        public void Dispose()
        {
        }
    }

    private sealed class CallbackLogger(string categoryName, Action<string> sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            string message = formatter(state, exception);
            if (!string.IsNullOrWhiteSpace(message))
            {
                sink($"{logLevel}: {categoryName}: {message}");
            }
        }
    }
}
