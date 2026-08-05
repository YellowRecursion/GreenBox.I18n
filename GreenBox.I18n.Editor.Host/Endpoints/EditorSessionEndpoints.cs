using GreenBox.I18n.Editor.Host.Editor;

namespace GreenBox.I18n.Editor.Host.Endpoints;

/// <summary>
/// Registers endpoints related to the editor session.
/// </summary>
public static class EditorSessionEndpoints
{
    /// <summary>
    /// Maps editor session endpoints.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The supplied endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapEditorSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/session", (EditorSession session) => session.GetSnapshot());
        return endpoints;
    }
}
