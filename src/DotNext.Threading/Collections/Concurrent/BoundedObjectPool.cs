using System.Runtime.CompilerServices;

namespace DotNext.Collections.Concurrent;

/// <summary>
/// Represents object pool of the fixed size.
/// </summary>
/// <typeparam name="T">The type of the objects in the pool.</typeparam>
public sealed class BoundedObjectPool<T> : IObjectPool<T>
    where T : class
{
    private RingBuffer<T> buffer;

    /// <summary>
    /// Initializes a new object pool.
    /// </summary>
    /// <param name="desiredCapacity">The desired number of the objects that can be retained by the pool. The value is rounded to the power of 2 by the pool.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="desiredCapacity"/> is negative or greater than <see cref="Array.MaxLength"/>.</exception>
    public BoundedObjectPool(int desiredCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)desiredCapacity, (uint)Array.MaxLength, nameof(desiredCapacity));

        buffer = new(desiredCapacity);
    }

    /// <summary>
    /// Gets the capacity of the pool.
    /// </summary>
    public int Capacity => buffer.Length;

    /// <summary>
    /// Tries to rent the object.
    /// </summary>
    /// <returns>The object instance; or <see langword="null"/> if there are no available objects in the pool.</returns>
    public T? TryGet()
    {
        ref var slot = ref buffer.TryDequeue(out var sequence);
        T? result;
        if (Unsafe.IsNullRef(ref slot))
        {
            result = null;
        }
        else
        {
            result = slot.Item;
            slot.Item = null;
            slot.Sequence = sequence;
        }

        return result;
    }

    /// <summary>
    /// Returns the object to this pool.
    /// </summary>
    /// <param name="item">The object that becomes available for the rent.</param>
    /// <returns>
    /// <see langword="true"/> if the object is returned to the pool;
    /// otherwise, <see langword="false"/> if there is no free space in the pool.</returns>
    public bool TryReturn(T item)
    {
        ref var slot = ref buffer.TryEnqueue(out var sequence);
        if (Unsafe.IsNullRef(ref slot))
            return false;

        slot.Item = item;
        slot.Sequence = sequence;
        return true;
    }

    void IObjectPool<T>.Return(T item) => TryReturn(item);

    /// <summary>
    /// Freezes the pool in a way when the object cannot be returned back to the pool.
    /// </summary>
    /// <remarks>
    /// Any subsequent call to the <see cref="TryReturn"/> method returns <see langword="false"/>.
    /// </remarks>
    public void Freeze() => buffer.Freeze();

    /// <summary>
    /// Gets a value indicating that the pool is frozen and the object cannot be returned back to the pool.
    /// </summary>
    public bool IsFrozen => buffer.IsFrozen;
}