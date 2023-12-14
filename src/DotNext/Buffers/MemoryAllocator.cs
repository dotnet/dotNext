namespace DotNext.Buffers;

/// <summary>
/// Represents memory allocator.
/// </summary>
/// <param name="length">The number of items in the rented memory.</param>
/// <typeparam name="T">The type of the items in the memory pool.</typeparam>
/// <returns>The rented memory.</returns>
public delegate MemoryOwner<T> MemoryAllocator<T>(int length);