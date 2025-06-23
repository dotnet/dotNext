using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.InteropServices;

using Collections.Generic;

partial struct Pointer<T>
{
    /// <summary>
    /// Represents enumerator over raw memory.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public unsafe struct Enumerator : IEnumerator<Enumerator, T>
    {
        private readonly T* ptr;
        private readonly nuint count;
        private nuint index;

        internal Enumerator(T* ptr, nuint count)
        {
            this.count = count > 0 ? count : throw new ArgumentOutOfRangeException(nameof(count));
            this.ptr = ptr;
            index = nuint.MaxValue;
        }

        /// <summary>
        /// Pointer to the currently enumerating element.
        /// </summary>
        public readonly Pointer<T> Pointer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(ptr + index);
        }

        /// <summary>
        /// Current element.
        /// </summary>
        public readonly T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ptr[index];
        }

        /// <summary>
        /// Adjust pointer.
        /// </summary>
        /// <returns><see langword="true"/>, if next element is available; <see langword="false"/>, if end of sequence reached.</returns>
        public bool MoveNext() => ptr is not null && ++index < count;

        /// <summary>
        /// Sets the enumerator to its initial position.
        /// </summary>
        public void Reset() => index = nuint.MaxValue;
    }
    
    /// <summary>
    /// Gets enumerator over raw memory.
    /// </summary>
    /// <param name="length">A number of elements to iterate.</param>
    /// <returns>Iterator object.</returns>
    [CLSCompliant(false)]
    public unsafe Enumerator GetEnumerator(nuint length) => IsNull ? default : new Enumerator(value, length);
}