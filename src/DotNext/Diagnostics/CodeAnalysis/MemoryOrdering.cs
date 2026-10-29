namespace DotNext.Diagnostics.CodeAnalysis;

/// <summary>
/// Represents memory ordering guarantees provided by the marked method.
/// </summary>
/// <seealso cref="MemoryOrderingAttribute"/>
public enum MemoryOrdering
{
    /// <summary>
    /// The memory access guarantees are unknown.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// No read or write that appears after the operation in the code can be moved before it.
    /// </summary>
    /// <remarks>
    /// This is the ordering of <see cref="Volatile.Read{T}(ref readonly T)"/>.
    /// Unlike <see cref="Volatile.ReadBarrier()"/>, it applies to the operation itself
    /// rather than to all the preceding reads.
    /// </remarks>
    Acquire,

    /// <summary>
    /// No read or write that appears before the operation in the code can be moved after it.
    /// </summary>
    /// <remarks>
    /// This is the ordering of <see cref="Volatile.Write{T}(ref T, T)"/>.
    /// Unlike <see cref="Volatile.WriteBarrier()"/>, it applies to the operation itself
    /// rather than to all the subsequent writes.
    /// </remarks>
    Release,

    /// <summary>
    /// The operation provides both <see cref="Acquire"/> and <see cref="Release"/> guarantees.
    /// </summary>
    AcquireRelease,

    /// <summary>
    /// The operation acts as a full memory barrier: no read or write can be moved across it in either direction.
    /// </summary>
    /// <remarks>
    /// This is the ordering of the methods of <see cref="Interlocked"/> class,
    /// such as <see cref="Interlocked.MemoryBarrier()"/>.
    /// </remarks>
    SequentiallyConsistent,
}
