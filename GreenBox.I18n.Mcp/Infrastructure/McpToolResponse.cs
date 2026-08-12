namespace GreenBox.I18n.Mcp;

public sealed record McpToolResponse<T>(bool Success, T? Data, McpToolError? Error)
{
    public static McpToolResponse<T> Ok(T data) => new(true, data, null);

    public static McpToolResponse<T> Failed(McpToolError error) => new(false, default, error);
}

public sealed record McpToolError(
    string Code,
    string Message,
    bool Retryable,
    string Remediation);

internal sealed class HostApiException : Exception
{
    public HostApiException(string code, string message, bool retryable, string remediation)
        : base(message)
    {
        Error = new McpToolError(code, message, retryable, remediation);
    }

    public McpToolError Error { get; }
}
