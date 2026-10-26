using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

using Runtime;
using Runtime.CompilerServices;

/// <summary>
/// Represents unified representation of the memory rented using various
/// types of memory pools.
/// </summary>
/// <typeparam name="T">The type of the items in the memory pool.</typeparam>
[StructLayout(LayoutKind.Auto)]
public partial struct MemoryOwner<T> : IMemoryOwner<T>, ISupplier<Memory<T>>, ISupplier<ReadOnlyMemory<T>>
{
    private readonly Ownership ownership;
    private int length;

    internal MemoryOwner(ArrayPool<T>? pool, T[] array, int length)
        : this(new Ownership(pool, array), length)
        => Debug.Assert(array.Length >= length);

    internal MemoryOwner(ArrayPool<T> pool, int length, bool exactSize)
    {
        Debug.Assert(pool is not null);

        if (length is 0)
        {
            this = default;
        }
        else
        {
            var array = pool.Rent(length);
            ownership = new(pool, array);
            this.length = exactSize ? length : array.Length;
        }
    }

    /// <summary>
    /// Rents the array from the pool.
    /// </summary>
    /// <param name="pool">The array pool.</param>
    /// <param name="length">The length of the array.</param>
    public MemoryOwner(ArrayPool<T> pool, int length)
        : this(pool, length, exactSize: true)
    {
    }

    /// <summary>
    /// Rents the memory from the pool.
    /// </summary>
    /// <param name="pool">The memory pool.</param>
    /// <param name="length">The number of elements to rent; or <c>-1</c> to rent default amount of memory.</param>
    public MemoryOwner(MemoryPool<T> pool, int length = -1)
    {
        if (length is 0)
        {
            this = default;
        }
        else
        {
            var owner = pool.Rent(length);
            if ((this.length = length < 0 ? owner.Memory.Length : length) > 0)
            {
                ownership = new(owner);
            }
            else
            {
                owner.Dispose();
                ownership = default;
            }
        }
    }

    /// <summary>
    /// Rents the memory.
    /// </summary>
    /// <param name="provider">The memory provider.</param>
    /// <param name="length">The number of elements to rent.</param>
    public MemoryOwner(Func<int, IMemoryOwner<T>> provider, int length)
    {
        switch (length)
        {
            case < 0:
                throw new ArgumentOutOfRangeException(nameof(length));
            case 0:
                this = default;
                break;
            default:
                var owner = provider(length);
                if ((this.length = int.Min(owner.Memory.Length, length)) > 0)
                {
                    ownership = new(owner);
                }
                else
                {
                    owner.Dispose();
                    ownership = default;
                }

                break;
        }
    }

    /// <summary>
    /// Rents the memory.
    /// </summary>
    /// <param name="provider">The memory provider.</param>
    public MemoryOwner(Func<IMemoryOwner<T>> provider)
    {
        var owner = provider();

        if ((length = owner.Memory.Length) > 0)
        {
            ownership = new(owner);
        }
        else
        {
            owner.Dispose();
            ownership = default;
        }
    }

    /// <summary>
    /// Wraps the array as if it was rented.
    /// </summary>
    /// <param name="array">The array to wrap.</param>
    /// <param name="length">The length of the array.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than 0 or greater than the length of <paramref name="array"/>.</exception>
    public MemoryOwner(T[] array, int length)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)length, (uint)array.Length, nameof(length));

        ownership = length > 0 ? new(owner: null, array) : default;
        this.length = length;
    }

    /// <summary>
    /// Wraps the array as if it was rented.
    /// </summary>
    /// <param name="array">The array to wrap.</param>
    public MemoryOwner(T[] array)
    {
        ownership = array is not [] ? new(owner: null, array) : default;
        length = array.Length;
    }

    /// <summary>
    /// Gets numbers of elements in the rented memory block.
    /// </summary>
    public readonly int Length => length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Truncate(int newLength)
    {
        Debug.Assert(newLength > 0);
        Debug.Assert(newLength <= ownership.Length);

        length = int.Min(length, newLength);

        AssertState();
    }

    /// <summary>
    /// Attempts to resize this buffer without reallocation.
    /// </summary>
    /// <remarks>
    /// This method always return <see langword="true"/> if <paramref name="newLength"/> is less than
    /// or equal to <see cref="Length"/>.
    /// </remarks>
    /// <param name="newLength">The requested length of this buffer.</param>
    /// <returns><see langword="true"/> if this buffer is resized successfully; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="newLength"/> is less than zero.</exception>
    public bool TryResize(int newLength)
    {
        switch (newLength)
        {
            case < 0:
                throw new ArgumentOutOfRangeException(nameof(newLength));
            case 0:
                Dispose();
                break;
            default:
                if (newLength > ownership.Length)
                    return false;

                length = newLength;
                break;
        }

        AssertState();
        return true;
    }

    /// <summary>
    /// Determines whether this memory is empty.
    /// </summary>
    public readonly bool IsEmpty => length is 0;

    /// <summary>
    /// Gets the memory belonging to this owner.
    /// </summary>
    /// <value>The memory belonging to this owner.</value>
    public readonly Memory<T> Memory
    {
        get
        {
            AssertState();

            return ownership.Memory.Slice(0, length);
        }
    }

    /// <summary>
    /// Gets the span over the memory belonging to this owner.
    /// </summary>
    /// <value>The span over the memory belonging to this owner.</value>
    public readonly Span<T> Span
    {
        get
        {
            AssertState();

            return ownership.Span.Slice(0, length);
        }
    }

    [Conditional("DEBUG")]
    private readonly void AssertState() => ownership.AssertState(length);

    /// <inheritdoc/>
    readonly Memory<T> ISupplier<Memory<T>>.Invoke() => Memory;

    /// <inheritdoc/>
    readonly ReadOnlyMemory<T> ISupplier<ReadOnlyMemory<T>>.Invoke() => Memory;

    /// <inheritdoc cref="IFunctional.DynamicInvoke"/>
    readonly void IFunctional.DynamicInvoke(ref readonly Variant args, int count, scoped Variant result)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(count, 0);

        if (result.TargetType == typeof(Memory<T>))
        {
            result.Mutable<Memory<T>>() = Memory;
        }
        else
        {
            result.Mutable<ReadOnlyMemory<T>>() = Memory;
        }
    }

    /// <summary>
    /// Gets managed pointer to the first element in the rented memory block.
    /// </summary>
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public readonly ref T DataRef => ref ownership.DataRef;

    /// <summary>
    /// Gets managed pointer to the item in the rented memory.
    /// </summary>
    /// <param name="index">The index of the element in memory.</param>
    /// <value>The managed pointer to the item.</value>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is invalid.</exception>
    public readonly ref T this[int index]
    {
        get
        {
            AssertState();
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)length, nameof(index));

            return ref Unsafe.Add(ref DataRef, index);
        }
    }

    /// <summary>
    /// Releases rented memory.
    /// </summary>
    public void Dispose()
    {
        ownership.Clear(RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        this = default;
    }

    /// <inheritdoc/>
    public readonly override string ToString() => Span.ToString();
}