using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers
{
    /// <summary>
    /// Represents unified representation of the memory rented using various
    /// types of memory pools.
    /// </summary>
    /// <typeparam name="T">The type of the items in the memory pool.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct MemoryOwner<T> : IMemoryOwner<T>, ISupplier<Memory<T>>
    {
        // Of type ArrayPool<T> or IMemoryOwner<T>.
        // If support of another type is needed then reconsider implementation
        // of Memory, this[nint index] and Expand members
        private readonly object? owner;
        private readonly T[]? array;  // not null only if owner is ArrayPool or null
        private readonly int length; // TODO: must be native integer in .NET 6

        internal MemoryOwner(ArrayPool<T>? pool, T[] array, int length)
        {
            Debug.Assert(array.Length >= length);
            this.array = array;
            owner = pool;
            this.length = length;
        }

        internal MemoryOwner(ArrayPool<T> pool, int length, bool exactSize)
        {
            array = pool.Rent(length);
            owner = pool;
            this.length = exactSize ? length : array.Length;
        }

        /// <summary>
        /// Rents the array from the pool.
        /// </summary>
        /// <param name="pool">The array pool.</param>
        /// <param name="length">The length of the array.</param>
        public MemoryOwner(ArrayPool<T> pool, int length)
            : this(pool, length, true)
        {
        }

        /// <summary>
        /// Rents the memory from the pool.
        /// </summary>
        /// <param name="pool">The memory pool.</param>
        /// <param name="length">The number of elements to rent; or <c>-1</c> to rent default amount of memory.</param>
        public MemoryOwner(MemoryPool<T> pool, int length = -1)
        {
            array = null;
            IMemoryOwner<T> owner;
            this.owner = owner = pool.Rent(length);
            this.length = length < 0 ? owner.Memory.Length : length;
        }

        /// <summary>
        /// Retns the memory.
        /// </summary>
        /// <param name="provider">The memory provider.</param>
        /// <param name="length">The number of elements to rent.</param>
        public MemoryOwner(Func<int, IMemoryOwner<T>> provider, int length)
        {
            array = null;
            IMemoryOwner<T> owner;
            this.owner = owner = provider(length);
            this.length = Math.Min(owner.Memory.Length, length);
        }

        /// <summary>
        /// Rents the memory.
        /// </summary>
        /// <param name="provider">The memory provider.</param>
        public MemoryOwner(Func<IMemoryOwner<T>> provider)
        {
            array = null;
            IMemoryOwner<T> owner;
            this.owner = owner = provider();
            length = owner.Memory.Length;
        }

        /// <summary>
        /// Wraps the array as if it was rented.
        /// </summary>
        /// <param name="array">The array to wrap.</param>
        /// <param name="length">The length of the array.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than 0 or greater than the length of <paramref name="array"/>.</exception>
        public MemoryOwner(T[] array, int length)
        {
            if (length > array.Length || length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));
            this.array = array;
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
        public int Length => length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UnsafeSetLength(int value) => Unsafe.AsRef(in length) = value;

        // WARNING: This is mutable method and should be used with care
        internal void Expand()
        {
            int length;

            if (array is not null)
                length = array.Length;
            else if (owner is not null)
                length = Unsafe.As<IMemoryOwner<T>>(owner).Memory.Length;
            else
                goto exit;

            UnsafeSetLength(length);

            exit:
            return;
        }

        internal MemoryOwner<T> Truncate(int newLength)
        {
            Debug.Assert(newLength > 0);
            MemoryOwner<T> result = this;
            if (newLength < length)
            {
                result.UnsafeSetLength(newLength);
            }

            return result;
        }

        /// <summary>
        /// Determines whether this memory is empty.
        /// </summary>
        public bool IsEmpty => length == 0;

        /// <summary>
        /// Gets the memory belonging to this owner.
        /// </summary>
        /// <value>The memory belonging to this owner.</value>
        public Memory<T> Memory
        {
            get
            {
                Memory<T> result;
                if (array is not null)
                    result = new Memory<T>(array);
                else if (owner is not null)
                    result = Unsafe.As<IMemoryOwner<T>>(owner).Memory;
                else
                    result = default;

                return result.Slice(0, length);
            }
        }

        /// <summary>
        /// Tries to get an array segment from the underlying memory buffer.
        /// </summary>
        /// <param name="segment">The array segment retrieved from the underlying memory buffer.</param>
        /// <returns><see langword="true"/> if the method call succeeds; <see langword="false"/> otherwise.</returns>
        public bool TryGetArray(out ArraySegment<T> segment)
        {
            if (array is not null)
            {
                segment = new ArraySegment<T>(array, 0, length);
                return true;
            }

            if (owner is IMemoryOwner<T> memory)
                return MemoryMarshal.TryGetArray(memory.Memory, out segment);

            segment = default;
            return false;
        }

        /// <inheritdoc/>
        Memory<T> ISupplier<Memory<T>>.Invoke() => Memory;

        /// <summary>
        /// Gets managed pointer to the item in the rented memory.
        /// </summary>
        /// <param name="index">The index of the element in memory.</param>
        /// <value>The managed pointer to the item.</value>
        public ref T this[nint index]
        {
            get
            {
                if (index < 0 || index >= length)
                    goto invalid_index;

                ref var result = ref Unsafe.NullRef<T>();
                if (array is not null)
#if NETSTANDARD2_1
                    result = ref array[0];
#else
                    result = ref MemoryMarshal.GetArrayDataReference(array);
#endif
                else if (owner is not null)
                    result = ref MemoryMarshal.GetReference(Unsafe.As<IMemoryOwner<T>>(owner).Memory.Span);
                else
                    goto invalid_index;

                return ref Unsafe.Add(ref result, index);

                invalid_index:
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        /// <summary>
        /// Gets managed pointer to the item in the rented memory.
        /// </summary>
        /// <param name="index">The index of the element in memory.</param>
        /// <value>The managed pointer to the item.</value>
        public ref T this[int index] => ref this[(nint)index];

        /// <summary>
        /// Releases rented memory.
        /// </summary>
        public void Dispose()
        {
            switch (owner)
            {
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
                case ArrayPool<T> pool:
                    Debug.Assert(array is not null);
                    pool.Return(array, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
                    break;
            }
        }
    }
}