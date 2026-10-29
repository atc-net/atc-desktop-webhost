namespace Atc.Desktop.WebHost.Bridge;

/// <summary>
/// An error a handler reports to the frontend. The code and message reach the page as
/// <c>error.code</c> and <c>error.message</c>. Other exceptions are reported as <c>handler_error</c>.
/// </summary>
public sealed class BridgeException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="BridgeException"/> class.</summary>
    public BridgeException()
        : this("handler_error", "The request failed.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BridgeException"/> class.</summary>
    /// <param name="message">The message.</param>
    public BridgeException(string message)
        : this("handler_error", message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BridgeException"/> class.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public BridgeException(
        string message,
        Exception innerException)
        : base(message, innerException)
        => Code = "handler_error";

    /// <summary>Initializes a new instance of the <see cref="BridgeException"/> class.</summary>
    /// <param name="code">A stable machine-readable code, e.g. "not_found".</param>
    /// <param name="message">The message.</param>
    public BridgeException(
        string code,
        string message)
        : base(message)
        => Code = code;

    /// <summary>Gets the machine-readable error code.</summary>
    public string Code { get; }
}