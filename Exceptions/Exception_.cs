namespace Gradual.Exceptions;

/// <summary>
/// Base exception for project-related errors
/// </summary>
public class Exception_ : Exception
{
    public Exception_() { }

    public Exception_(string message) : base(message) { }

    public Exception_(string message, Exception innerException) 
        : base(message, innerException) { }
}
