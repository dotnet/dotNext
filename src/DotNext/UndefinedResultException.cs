namespace DotNext;

/// <summary>
/// Indicates that the result of the operation is unavailable.
/// </summary>
/// <typeparam name="TError">The type of the error code.</typeparam>
public sealed class UndefinedResultException<TError> : Exception
    where TError : struct, Enum
{
    internal UndefinedResultException(TError errorCode)
        : base(ExceptionMessages.NoResult(errorCode))
        => ErrorCode = errorCode;

    /// <summary>
    /// Gets the error code associated with the exception.
    /// </summary>
    public TError ErrorCode { get; }
}