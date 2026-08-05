using GreenBox.I18n.Editor.Host.Contracts;

namespace GreenBox.I18n.Editor.Host.Editor;

/// <summary>
/// Owns the server-side state of the current editor session.
/// </summary>
public sealed class EditorSession
{
    private readonly Lock _lock = new();
    private long _revision = 0;

    /// <summary>
    /// Creates an immutable snapshot of the current session state.
    /// </summary>
    /// <returns>The current session snapshot.</returns>
    public EditorSessionResponse GetSnapshot()
    {
        lock (_lock)
        {
            return new EditorSessionResponse(false, _revision);
        }
    }
}
