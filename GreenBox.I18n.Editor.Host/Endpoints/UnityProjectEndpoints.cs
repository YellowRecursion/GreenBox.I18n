using GreenBox.I18n.Editor.Host.Contracts;
using GreenBox.I18n.Editor.Host.Editor;
using GreenBox.I18n.Editor.Host.Infrastructure;

namespace GreenBox.I18n.Editor.Host.Endpoints;

/// <summary>
/// Registers endpoints that describe the Unity project associated with the editor session.
/// </summary>
public static class UnityProjectEndpoints
{
    /// <summary>
    /// Maps Unity project endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapUnityProjectEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/api/unity-project",
            (EditorSession session, UnityProjectPresenceService presence) =>
                presence.GetStatus(session.GetSnapshot().CatalogPath));
        return endpoints;
    }
}
