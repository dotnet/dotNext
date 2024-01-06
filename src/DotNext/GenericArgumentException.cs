namespace DotNext;

/// <summary>
/// The exception that is thrown when one of the generic arguments
/// provided to a type is not valid.
/// </summary>
/// <remarks>
/// Initializes a new exception.
/// </remarks>
/// <param name="genericParam">Incorrect actual generic argument.</param>
/// <param name="message">The error message that explains the reason for the exception.</param>
/// <param name="paramName">The name of generic parameter.</param>
public class GenericArgumentException(Type genericParam, string message, string? paramName = null) : ArgumentException(message, paramName is { Length: > 0 } ? paramName : genericParam.FullName)
{
    /// <summary>
    /// Generic argument.
    /// </summary>
    public Type Argument => genericParam;
}

/// <summary>
/// The exception that is thrown when one of the generic arguments
/// provided to a type is not valid.
/// </summary>
/// <typeparam name="T">Captured generic argument treated as invalid.</typeparam>
/// <remarks>
/// Initializes a new exception.
/// </remarks>
/// <param name="message">The error message that explains the reason for the exception.</param>
/// <param name="paramName">The name of generic parameter.</param>
public class GenericArgumentException<T>(string message, string? paramName = null) : GenericArgumentException(typeof(T), message, paramName)
{
}