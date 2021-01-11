#if !NETSTANDARD2_1
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.InteropServices
{
    /// <summary>
    /// Represents pinned array that can be passed to unmanaged code
    /// without marshalling overhead.
    /// </summary>
    /// <typeparam name="T">The type of the array elements.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct PinnedArray<T> : IUnmanagedArray<T>, IList<T>, IReadOnlyList<T>, IEquatable<PinnedArray<T>>
        where T : unmanaged
    {
        private readonly T[]? array;

        /// <summary>
        /// Allocates a new pinned array in Pinned Object Heap.
        /// </summary>
        /// <param name="length">The length of the array.</param>
        /// <param name="zeroMem"><see langword="true"/> to allocate the array with zeroed content; otherwise, <see langword="false"/>.</param>
        public PinnedArray(int length, bool zeroMem = false)
        {
            if (length == 0)
                array = System.Array.Empty<T>();
            else if (zeroMem)
                array = GC.AllocateArray<T>(length, true);
            else
                array = GC.AllocateUninitializedArray<T>(length, true);
        }

        private PinnedArray(T[] array) => this.array = array;

        /// <inheritdoc />
        bool ICollection<T>.IsReadOnly => true;

        /// <summary>
        /// Creates deep copy of this array.
        /// </summary>
        /// <returns>The deep copy of this array.</returns>
        public PinnedArray<T> Clone()
        {
            if (array.IsNullOrEmpty())
                return default;

            var copy = GC.AllocateUninitializedArray<T>(array.Length, true);
            array.CopyTo(copy, 0);
            return new PinnedArray<T>(copy);
        }

        /// <inheritdoc />
        object ICloneable.Clone() => Clone();

        /// <summary>
        /// Gets managed pointer to the array element at the specified index.
        /// </summary>
        /// <param name="index">The index of the element in memory.</param>
        /// <value>The managed pointer to the array element.</value>
        public ref T this[long index]
        {
            get
            {
                if (array is null)
                    throw new ArgumentOutOfRangeException(nameof(index));

                return ref array[index];
            }
        }

        /// <inheritdoc />
        T IList<T>.this[int index]
        {
            get => this[index];
            set => this[index] = value;
        }

        /// <inheritdoc />
        T IReadOnlyList<T>.this[int index] => this[index];

        /// <summary>
        /// Returns the index of a specific item in this array.
        /// </summary>
        /// <param name="item">The object to locate in this array.</param>
        /// <returns>The index of <paramref name="item"/> if found in this array; otherwise, -1.</returns>
        public int IndexOf(T item) => System.Array.IndexOf(Array, item);

        /// <summary>
        /// Determines whether this array contains a specific value.
        /// </summary>
        /// <param name="item">The object to locate in this array.</param>
        /// <returns><see langword="true"/> if item is found in this array; otherwise, <see langword="false"/>.</returns>
        public bool Contains(T item) => Array.As<ICollection<T>>().Contains(item);

        /// <summary>
        /// Copies the elements of this array to another array, starting at a particular index in
        /// the destination array.
        /// </summary>
        /// <param name="destination">The array that is the destination of the elements copied from this array.</param>
        /// <param name="index">The zero-based index in the destination at which copying begins.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than 0.</exception>
        /// <exception cref="ArgumentException">The number of elements in this array is greater than the available space from <paramref name="index"/> to the end of the destination array.</exception>
        public void CopyTo(T[] destination, int index) => Array.CopyTo(destination, index);

        /// <inheritdoc />
        void IList<T>.Insert(int index, T item) => throw new NotSupportedException();

        /// <inheritdoc />
        void IList<T>.RemoveAt(int index) => throw new NotSupportedException();

        /// <inheritdoc />
        void ICollection<T>.Add(T item) => throw new NotSupportedException();

        /// <inheritdoc />
        void ICollection<T>.Clear() => throw new NotSupportedException();

        /// <inheritdoc />
        bool ICollection<T>.Remove(T item) => throw new NotSupportedException();

        /// <summary>
        /// Creates a stream over elements in this array.
        /// </summary>
        /// <returns>The stream over elements in this array.</returns>
        public unsafe Stream AsStream()
        {
            if (array.IsNullOrEmpty())
                return Stream.Null;
            if (typeof(T) == typeof(byte))
                return new MemoryStream(Unsafe.As<byte[]>(array), true);
            return new UnmanagedMemoryStream((byte*)RawPointer, Size);
        }

        /// <summary>
        /// Gets the span over array elements.
        /// </summary>
        public Span<T> Span => array.AsSpan();

        /// <summary>
        /// Represents underlying array as <see cref="Memory{T}"/>.
        /// </summary>
        public Memory<T> Memory => array.IsNullOrEmpty() ? Memory<T>.Empty : MemoryMarshal.CreateFromPinnedArray(array, 0, array.Length);

        /// <inheritdoc />
        Span<byte> IUnmanagedMemory.Bytes => MemoryMarshal.AsBytes(Span);

        private unsafe void* RawPointer => array.IsNullOrEmpty() ? null : Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(array));

        /// <summary>
        /// Gets the pointer to the first element of the pinned array.
        /// </summary>
        public unsafe Pointer<T> Pointer => new Pointer<T>((T*)RawPointer);

        /// <inheritdoc />
        unsafe Pointer<byte> IUnmanagedMemory.Pointer => new Pointer<byte>((byte*)RawPointer);

        /// <summary>
        /// Gets length of the pinned array.
        /// </summary>
        public int Length => array?.Length ?? 0;

        /// <inheritdoc />
        int ICollection<T>.Count => Length;

        /// <inheritdoc />
        int IReadOnlyCollection<T>.Count => Length;

        private unsafe long Size => checked(Length * sizeof(T));

        /// <inheritdoc />
        long IUnmanagedMemory.Size => Size;

        /// <summary>
        /// Gets underlying array.
        /// </summary>
        public T[] Array => array ?? System.Array.Empty<T>();

        /// <inheritdoc />
        T[] IUnmanagedArray<T>.ToArray() => Array;

        /// <inheritdoc />
        T[] IConvertible<T[]>.Convert() => Array;

        /// <summary>
        /// Computes bitwise equality between this array and the specified managed array.
        /// </summary>
        /// <param name="other">The array to be compared.</param>
        /// <returns><see langword="true"/>, if both memory blocks have the same bytes; otherwise, <see langword="false"/>.</returns>
        public bool BitwiseEquals(T[] other) => OneDimensionalArray.BitwiseEquals(Array, other);

        /// <summary>
        /// Bitwise comparison of the memory blocks.
        /// </summary>
        /// <param name="other">The array to be compared.</param>
        /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
        public int BitwiseCompare(T[] other) => OneDimensionalArray.BitwiseCompare(Array, other);

        /// <summary>
        /// Gets underlying array.
        /// </summary>
        /// <param name="array">The pinned array.</param>
        /// <returns>The underlying array.</returns>
        public static implicit operator T[](PinnedArray<T> array) => array.Array;

        /// <summary>
        /// Gets enumerator over array elements.
        /// </summary>
        /// <returns>The enumerator over array elements.</returns>
        public IEnumerator<T> GetEnumerator() => Array.As<IEnumerable<T>>().GetEnumerator();

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <inheritdoc />
        void IDisposable.Dispose()
        {
        }

        /// <summary>
        /// Returns a string that represents the current array.
        /// </summary>
        /// <returns>A string that represents the current array.</returns>
        public override string? ToString() => Array.ToString();

        /// <summary>
        /// Determines whether the current object references the same array as other object.
        /// </summary>
        /// <param name="other">The array to be compared.</param>
        /// <returns><see langword="true"/> if the current object references the same array as <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public bool Equals(PinnedArray<T> other) => ReferenceEquals(Array, other.Array);

        /// <summary>
        /// Determines whether the current object references the same array as other object.
        /// </summary>
        /// <param name="other">The array to be compared.</param>
        /// <returns><see langword="true"/> if the current object references the same array as <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other switch
        {
            PinnedArray<T> pinned => Equals(pinned),
            T[] array => ReferenceEquals(Array, array),
            _ => false
        };

        /// <summary>
        /// Gets the identity of the referenced array.
        /// </summary>
        /// <returns>The identity of the referenced array.</returns>
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(Array);

        /// <summary>
        /// Determines whether the two objects represent the same array.
        /// </summary>
        /// <param name="first">The first array to be compared.</param>
        /// <param name="second">The second array to be compared.</param>
        /// <returns><see langword="true"/> if both objects represent the same array; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(PinnedArray<T> first, PinnedArray<T> second)
            => first.Equals(second);

        /// <summary>
        /// Determines whether the two objects represent different arrays.
        /// </summary>
        /// <param name="first">The first array to be compared.</param>
        /// <param name="second">The second array to be compared.</param>
        /// <returns><see langword="true"/> if both objects represent different arrays; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(PinnedArray<T> first, PinnedArray<T> second)
            => !first.Equals(second);
    }
}
#endif