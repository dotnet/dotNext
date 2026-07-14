namespace DotNext.Buffers;

/// <summary>
/// Indicates that the buffer capacity exceeded.
/// </summary>
public sealed class BufferSizeLimitExceededException : OutOfMemoryException
{
    internal BufferSizeLimitExceededException(ulong maxCapacity)
        : base(ExceptionMessages.BufferSizeLimitExceeded(maxCapacity))
    {
    }
}