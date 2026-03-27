using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext.Collections.Concurrent;

/// <summary>
/// Represents a pool of integer values.
/// </summary>
/// <remarks>
/// This type can be used to create a custom object pool. In contrast to <see cref="BoundedObjectPool{T}"/>,
/// the pool of integer values is pre-populated with the values after its construction. So the caller always
/// know whether the particular object is taken from the pool or not.
/// </remarks>
public sealed class IndexPool
{
    private RingBuffer<int> buffer;

    /// <summary>
    /// Initializes a new pool of the desired capacity.
    /// </summary>
    /// <param name="desiredCapacity">The desired number of values in the pool. The value is rounded to the power of 2 by the pool.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="desiredCapacity"/> is negative or greater than <see cref="Array.MaxLength"/>.</exception>
    public IndexPool(int desiredCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)desiredCapacity, (uint)Array.MaxLength, nameof(desiredCapacity));

        buffer = new(desiredCapacity);
        buffer.Populate();
    }

    /// <summary>
    /// Gets a value indicating that the pool is empty.
    /// </summary>
    public bool IsEmpty => buffer.IsEmpty;

    /// <summary>
    /// Gets the capacity of the pool.
    /// </summary>
    public int Capacity => buffer.Length;

    /// <summary>
    /// Tries to get the value from the pool.
    /// </summary>
    /// <param name="value">The value is in range [0..<see cref="Capacity"/>).</param>
    /// <returns><see langword="true"/> if value is taken from the pool successfully; <see langword="false"/> is this pool is empty.</returns>
    public bool TryGet(out int value)
    {
        ref var slot = ref buffer.TryDequeue(out var sequence);
        if (Unsafe.IsNullRef(in slot))
        {
            value = 0;
            return false;
        }

        value = slot.Item;
        slot.Sequence = sequence;
        return true;
    }

    /// <summary>
    /// Returns the value previously returned by <see cref="TryGet"/> back to the pool.
    /// </summary>
    /// <param name="value">The value to be returned.</param>
    /// <exception cref="OverflowException">
    /// <see cref="TryGet"/> and <see cref="Return"/> calls are unbalanced, so the caller is trying to return
    /// the value to this pool which is full.
    /// </exception>
    public void Return(int value)
    {
        ref var slot = ref buffer.TryEnqueue(out var sequence);
        if (Unsafe.IsNullRef(in slot))
            ThrowOverflowException();

        slot.Item = value;
        slot.Sequence = sequence;
        
        [DoesNotReturn]
        [StackTraceHidden]
        static void ThrowOverflowException() => throw new OverflowException();
    }

    /// <summary>
    /// Returns the value to this pool.
    /// </summary>
    /// <param name="value">The value to be returned.</param>
    /// <returns>
    /// <see langword="true"/> if the value is returned to the pool;
    /// otherwise, <see langword="false"/> if this pool is frozen.</returns>
    public bool TryReturn(int value)
    {
        ref var slot = ref buffer.TryEnqueue(out var sequence);
        if (Unsafe.IsNullRef(in slot))
            return false;

        slot.Item = value;
        slot.Sequence = sequence;
        return true;
    }

    /// <summary>
    /// Freezes the pool in a way when the object cannot be returned back to the pool.
    /// </summary>
    /// <remarks>
    /// Any subsequent call to the <see cref="TryReturn"/> method returns <see langword="false"/>.
    /// </remarks>
    /// <returns>
    /// <see langword="true"/> if this method is called for the first time;
    /// <see langword="false"/> if the pool is already frozen.
    /// </returns>
    public bool Freeze() => buffer.Freeze();
}

file static class RingBufferExtensions
{
    public static void Populate(this ref RingBuffer<int> buffer)
    {
        for (var index = 0; index < buffer.Length; index++)
        {
            ref var slot = ref buffer.TryEnqueue(out var sequence);
            Debug.Assert(!Unsafe.IsNullRef(in slot));
            
            slot.Item = index;
            slot.Sequence = sequence;
        }
    }
}