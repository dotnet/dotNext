using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

/// <summary>
/// Represents unified representation of the memory rented using various
/// types of memory pools.
/// </summary>
/// <typeparam name="T">The type of the items in the memory pool.</typeparam>
[StructLayout(LayoutKind.Auto)]
public struct MemoryOwner<T> : IMemoryOwner<T>, ISupplier<Memory<T>>, ISupplier<ReadOnlyMemory<T>>
{
    // Of type ArrayPool<T> or IMemoryOwner<T>.
    // If support of another type is needed then reconsider implementation
    // of Memory, this[nint index] and Expand members
    private readonly object? owner;
    private readonly T[]? array;  // not null only if owner is ArrayPool or null
    private int length;

    internal MemoryOwner(ArrayPool<T>? pool, T[] array, int length)
    {
        Debug.Assert(length > 0);
        Debug.Assert(array.Length >= length);

        this.array = array;
        owner = pool;
        this.length = length;
    }

    internal MemoryOwner(ArrayPool<T> pool, int length, [ConstantExpected] bool exactSize)
    {
        Debug.Assert(pool is not null);

        if (length is 0)
        {
            this = default;
        }
        else
        {
            array = pool.Rent(length);
            owner = pool;
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
            array = null;
            IMemoryOwner<T> owner = pool.Rent(length);
            if ((this.length = length < 0 ? owner.Memory.Length : length) > 0)
            {
                this.owner = owner;
            }
            else
            {
                owner.Dispose();
                this.owner = null;
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
                array = null;
                var owner = provider(length);
                if ((this.length = Math.Min(owner.Memory.Length, length)) > 0)
                {
                    this.owner = owner;
                }
                else
                {
                    owner.Dispose();
                    this.owner = null;
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
        array = null;
        var owner = provider();

        if ((length = owner.Memory.Length) > 0)
        {
            this.owner = owner;
        }
        else
        {
            owner.Dispose();
            this.owner = null;
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

        this.array = length > 0 ? array : null;
        this.length = length;
        owner = null;
    }

    /// <summary>
    /// Wraps the array as if it was rented.
    /// </summary>
    /// <param name="array">The array to wrap.</param>
    public MemoryOwner(T[] array)
        : this(array, array.Length)
    {
    }

    /// <summary>
    /// Gets numbers of elements in the rented memory block.
    /// </summary>
    public readonly int Length => length;

    internal void Expand()
    {
        length = RawLength;

        AssertValid();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Truncate(int newLength)
    {
        Debug.Assert(newLength > 0);
        Debug.Assert(newLength <= RawLength);

        length = Math.Min(length, newLength);

        AssertValid();
    }

    private readonly int RawLength
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => array?.Length ?? Unsafe.As<IMemoryOwner<T>>(owner)?.Memory.Length ?? 0;
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
                if (newLength > RawLength)
                    return false;

                length = newLength;
                break;
        }

        AssertValid();
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
            AssertValid();

            return array?.AsMemory(0, length)
                ?? Unsafe.As<IMemoryOwner<T>>(owner)?.Memory.Slice(0, length)
                ?? Memory<T>.Empty;
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
            AssertValid();

            return MemoryMarshal.CreateSpan(ref First, length);
        }
    }

    [Conditional("DEBUG")]
    private readonly void AssertValid()
    {
        Debug.Assert(IsEmpty ^ (array is not null || owner is not null));
        Debug.Assert(owner is null or ArrayPool<T> or IMemoryOwner<T>);
        Debug.Assert(array is null or { Length: > 0 });
        Debug.Assert(array is null ? owner is null or IMemoryOwner<T> : owner is null or ArrayPool<T>);
    }

    /// <inheritdoc/>
    readonly Memory<T> ISupplier<Memory<T>>.Invoke() => Memory;

    /// <inheritdoc/>
    readonly ReadOnlyMemory<T> ISupplier<ReadOnlyMemory<T>>.Invoke() => Memory;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal readonly ref T First
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref array is not null
                ? ref MemoryMarshal.GetArrayDataReference(array)
                : ref owner is not null
                ? ref MemoryMarshal.GetReference(Unsafe.As<IMemoryOwner<T>>(owner).Memory.Span)
                : ref Unsafe.NullRef<T>();
    }

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
            AssertValid();
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)length, nameof(index));

            return ref Unsafe.Add(ref First, index);
        }
    }

    internal readonly void Clear(bool clearBuffer)
    {
        if (array is null)
        {
            Debug.Assert(owner is null or IDisposable);
            Unsafe.As<IDisposable>(owner)?.Dispose();
        }
        else if (owner is not null)
        {
            Debug.Assert(owner is ArrayPool<T>);
            Unsafe.As<ArrayPool<T>>(owner).Return(array, clearBuffer);
        }
        else if (clearBuffer)
        {
            Array.Clear(array);
        }
    }

    /// <summary>
    /// Releases rented memory.
    /// </summary>
    public void Dispose()
    {
        Clear(RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        this = default;
    }

    /// <inheritdoc/>
    public override readonly string ToString() => Span.ToString();
}