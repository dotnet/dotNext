namespace DotNext.Runtime.InteropServices;

/// <summary>
/// The exception that is thrown when there is an attempt to dereference zero pointer.
/// </summary>
/// <param name="message">The human-readable description of this message.</param>
public sealed class NullPointerException(string message) : NullReferenceException(message)
{
    /// <summary>
    /// Initializes a new exception representing attempt to dereference zero pointer.
    /// </summary>
    public NullPointerException()
        : this(ExceptionMessages.NullPtr)
    {
    }
}