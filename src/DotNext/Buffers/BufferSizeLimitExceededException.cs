namespace DotNext.Buffers;

/// <summary>
/// Indicates that the buffer capacity exceeded.
/// </summary>
public sealed class BufferSizeLimitExceededException() : OutOfMemoryException(ExceptionMessages.BufferSizeLimitExceeded);