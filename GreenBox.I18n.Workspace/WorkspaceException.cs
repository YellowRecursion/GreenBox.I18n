namespace GreenBox.I18n.Workspace;

public sealed class WorkspaceException : Exception
{
    public WorkspaceException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
