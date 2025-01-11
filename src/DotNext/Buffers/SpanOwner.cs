﻿using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

using Intrinsics = Runtime.Intrinsics;

/// <summary>
/// Represents the memory obtained from the pool or allocated
/// on the stack or heap.
/// </summary>
/// <remarks>
/// This type is aimed to be compatible with memory allocated using <c>stackalloc</c> operator.
/// If stack allocation threshold is reached (e.g. <see cref="StackallocThreshold"/>) then it's possible to use pooled memory from
/// arbitrary <see cref="MemoryPool{T}"/> or <see cref="ArrayPool{T}.Shared"/>. Custom
/// <see cref="ArrayPool{T}"/> is not supported because default <see cref="ArrayPool{T}.Shared"/>
/// is optimized for per-CPU core allocation which is perfect when the same
/// thread is responsible for renting and releasing the array.
/// </remarks>
/// <example>
/// <code>
/// const int stackallocThreshold = 20;
/// var memory = size &lt;=stackallocThreshold ? new SpanOwner&lt;byte&gt;(stackalloc byte[stackallocThreshold], size) : new SpanOwner&lt;byte&gt;(size);
/// </code>
/// </example>
/// <typeparam name="T">The type of the elements in the rented memory.</typeparam>
[StructLayout(LayoutKind.Auto)]
public ref struct SpanOwner<T>
{
    /// <summary>
    /// Global recommended number of elements that can be allocated on the stack.
    /// </summary>
    /// <remarks>
    /// This property is for internal purposes only and should not be referenced
    /// directly in your code.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [CLSCompliant(false)]
    public static int StackallocThreshold { get; } = 1 + (LibrarySettings.StackallocThreshold / Unsafe.SizeOf<T>());

    private readonly object? owner;
    private readonly Span<T> memory;

    /// <summary>
    /// Rents the memory referenced by the span.
    /// </summary>
    /// <param name="span">The span that references the memory to rent.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SpanOwner(Span<T> span)
        => memory = span;

    /// <summary>
    /// Rents the memory referenced by the span.
    /// </summary>
    /// <param name="span">The span that references the memory to rent.</param>
    /// <param name="length">The actual length of the data.</param>
    public SpanOwner(Span<T> span, int length)
        : this(span.Slice(0, length))
    {
    }

    /// <summary>
    /// Rents the memory from the pool.
    /// </summary>
    /// <param name="pool">The memory pool.</param>
    /// <param name="minBufferSize">The minimum size of the memory to rent.</param>
    /// <param name="exactSize"><see langword="true"/> to return the buffer of <paramref name="minBufferSize"/> length; otherwise, the returned buffer is at least of <paramref name="minBufferSize"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="pool"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minBufferSize"/> is less than or equal to zero.</exception>
    public SpanOwner(MemoryPool<T> pool, int minBufferSize, bool exactSize = true)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minBufferSize);

        var owner = pool.Rent(minBufferSize);
        memory = owner.Memory.Span;
        if (exactSize)
            memory = memory.Slice(0, minBufferSize);
        this.owner = owner;
    }

    /// <summary>
    /// Rents the memory from the pool.
    /// </summary>
    /// <param name="pool">The memory pool.</param>
    /// <exception cref="ArgumentNullException"><paramref name="pool"/> is <see langword="null"/>.</exception>
    public SpanOwner(MemoryPool<T> pool)
    {
        ArgumentNullException.ThrowIfNull(pool);
        var owner = pool.Rent();
        memory = owner.Memory.Span;
        this.owner = owner;
    }

    /// <summary>
    /// Rents the memory from <see cref="ArrayPool{T}.Shared"/>, if <typeparamref name="T"/>
    /// contains at least one field of reference type; or use <see cref="NativeMemory"/>.
    /// </summary>
    /// <param name="minBufferSize">The minimum size of the memory to rent.</param>
    /// <param name="exactSize"><see langword="true"/> to return the buffer of <paramref name="minBufferSize"/> length; otherwise, the returned buffer is at least of <paramref name="minBufferSize"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minBufferSize"/> is less than or equal to zero.</exception>
    public SpanOwner(int minBufferSize, bool exactSize = true)
    {
        if (UseNativeAllocation)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minBufferSize);
            memory = Allocate(minBufferSize);
            owner = Sentinel.Instance;
        }
        else
        {
            var owner = ArrayPool<T>.Shared.Rent(minBufferSize);
            memory = exactSize ? new(owner, 0, minBufferSize) : new(owner);
            this.owner = owner;
        }

        static unsafe Span<T> Allocate(int length)
        {
            void* ptr;

            if (IsNaturalAlignment)
            {
                ptr = NativeMemory.Alloc((uint)length, (uint)Unsafe.SizeOf<T>());
            }
            else
            {
                var byteCount = checked((uint)Unsafe.SizeOf<T>() * (nuint)(uint)length);
                ptr = NativeMemory.AlignedAlloc(byteCount, (uint)Intrinsics.AlignOf<T>());
            }

            return new(ptr, length);
        }
    }

    private static bool IsNaturalAlignment => Intrinsics.AlignOf<T>() <= nuint.Size;

    private static bool UseNativeAllocation
        => LibrarySettings.UseNativeAllocation && !RuntimeHelpers.IsReferenceOrContainsReferences<T>();

    /// <summary>
    /// Gets the rented memory.
    /// </summary>
    public readonly Span<T> Span => memory;

    /// <summary>
    /// Gets a value indicating that this object
    /// doesn't reference rented memory.
    /// </summary>
    public readonly bool IsEmpty => memory.IsEmpty;

    /// <summary>
    /// Converts the reference to the already allocated memory
    /// into the rental object.
    /// </summary>
    /// <param name="span">The allocated memory to convert.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator SpanOwner<T>(Span<T> span)
        => new(span);

    /// <summary>
    /// Gets length of the rented memory.
    /// </summary>
    public readonly int Length => memory.Length;

    /// <summary>
    /// Gets the memory element by its index.
    /// </summary>
    /// <param name="index">The index of the memory element.</param>
    /// <returns>The managed pointer to the memory element.</returns>
    public readonly ref T this[int index] => ref memory[index];

    /// <summary>
    /// Obtains managed pointer to the first element of the rented array.
    /// </summary>
    /// <returns>The managed pointer to the first element of the rented array.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ref T GetPinnableReference() => ref memory.GetPinnableReference();

    /// <summary>
    /// Gets textual representation of the rented memory.
    /// </summary>
    /// <returns>The textual representation of the rented memory.</returns>
    public readonly override string ToString() => memory.ToString();

    /// <summary>
    /// Returns the memory back to the pool.
    /// </summary>
    public void Dispose()
    {
        if (owner is T[] array)
        {
            ArrayPool<T>.Shared.Return(array, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
        else if (UseNativeAllocation && ReferenceEquals(owner, Sentinel.Instance))
        {
            unsafe
            {
                var ptr = Unsafe.AsPointer(ref MemoryMarshal.GetReference(memory));
                if (IsNaturalAlignment)
                {
                    NativeMemory.Free(ptr);
                }
                else
                {
                    NativeMemory.AlignedFree(ptr);
                }
            }
        }
        else
        {
            Unsafe.As<IDisposable>(owner)?.Dispose();
        }

        this = default;
    }
}