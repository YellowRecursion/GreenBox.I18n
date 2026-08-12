using GreenBox.I18n.Editor.Host.Contracts;
using GreenBox.I18n.Editor.Host.Editor;
using GreenBox.I18n.Editor.Host.Infrastructure;

namespace GreenBox.I18n.Editor.Host.Endpoints;

/// <summary>
/// Registers endpoints for personal editor preferences.
/// </summary>
public static class EditorPreferencesEndpoints
{
    /// <summary>
    /// Maps personal editor preference endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapEditorPreferencesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder preferenceEndpoints = endpoints.MapGroup("/api/preferences");
        preferenceEndpoints.MapGet(string.Empty, (EditorPreferencesStore store) => store.GetSnapshot());
        preferenceEndpoints.MapPost(string.Empty, Update);
        return endpoints;
    }

    private static IResult Update(
        UpdateEditorPreferencesRequest request,
        EditorPreferencesStore store)
    {
        try
        {
            return Results.Ok(store.Update(request));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Results.Json(
                new EditorErrorResponse(HostErrorCodes.PreferencesWriteFailed, exception.Message),
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }
}
