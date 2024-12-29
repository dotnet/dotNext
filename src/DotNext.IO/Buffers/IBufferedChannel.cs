using System.Buffers;

namespace DotNext.Buffers;

/// <summary>
/// Represents buffered reader or writer.
/// </summary>
public interface IBufferedChannel : IResettable, IDisposable
{
    /// <summary>
    /// Gets buffer allocator.
    /// </summary>
    MemoryAllocator<byte>? Allocator { get; init; }
    
    /// <summary>
    /// Gets the maximum size of the internal buffer.
    /// </summary>
    int MaxBufferSize { get; init; }
}