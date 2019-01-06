using System;
using System.Collections.Generic;
using static System.Runtime.InteropServices.Marshal;
using System.Runtime.CompilerServices;
using System.Buffers;

namespace Cheats
{
    /// <summary>
    /// Represents unmanaged type boxed into unmanaged memory (located out of managed heap)
    /// </summary>
    /// <typeparam name="T">Type to be allocated in the unmanaged heap.</typeparam>
    public unsafe readonly struct OffHeapBuffer<T>: IDisposable, IBox<T>, IEquatable<OffHeapBuffer<T>>
        where T: unmanaged
    {
        public static readonly int Size = SizeOf<T>();

        private readonly T* pointer;

        private OffHeapBuffer(T* pointer)
            => this.pointer = pointer;
        
        private OffHeapBuffer(IntPtr pointer)
            : this((T*)pointer)
        {
        }

        private static OffHeapBuffer<T> AllocUnitialized() => new OffHeapBuffer<T>(AllocHGlobal(Size));

        /// <summary>
        /// Boxes unmanaged type into unmanaged heap.
        /// </summary>
        /// <param name="value">A value to be placed into unmanaged memory.</param>
        /// <returns>Embedded reference to the allocated unmanaged memory.</returns>
        public unsafe static OffHeapBuffer<T> Box(T value)
        {
            //allocate unmanaged memory
            var result = AllocUnitialized();
            Unsafe.Copy(result.pointer, ref value);
            return result;
        }

        /// <summary>
        /// Allocates unmanaged type in the unmanaged heap.
        /// </summary>
        /// <returns>Embedded reference to the allocated unmanaged memory.</returns>
        public static OffHeapBuffer<T> Alloc()
        {
            var result = AllocUnitialized();
            result.InitMem(0);
            return result;
        }

        private void InitMem(byte value)
            => Unsafe.InitBlockUnaligned(pointer, 0, (uint)Size);
        
        /// <summary>
        /// Sets all bits of allocated memory to zero.
        /// </summary>
        public void ZeroMem()
        {
            if(pointer == Memory.NullPtr)
                throw NullPtrException();
            InitMem(0);
        }

        [CLSCompliant(false)]
        public void CopyFrom(T* source)
        {
            if(pointer == Memory.NullPtr)
                throw NullPtrException();
            else if(source == Memory.NullPtr)
                throw new ArgumentNullException(nameof(source));
            else
                Unsafe.CopyBlockUnaligned(pointer, source, (uint)Size);
        }

        public void CopyFrom(ref T source)
        {
            if(pointer == Memory.NullPtr)
                throw NullPtrException();
            else
                Unsafe.Copy(pointer, ref source);
        }

        [CLSCompliant(false)]
        public void CopyTo(T* destination)
        {
            if(pointer == Memory.NullPtr)
                throw NullPtrException();
            else if(destination == Memory.NullPtr)
                throw new ArgumentNullException(nameof(destination));
            else
                Unsafe.CopyBlockUnaligned(destination, pointer, (uint)Size);
        }

        public void CopyTo(ref T destination)
        {
            if(pointer == Memory.NullPtr)
                throw NullPtrException();
            else
                Unsafe.Copy(ref destination, pointer);
        }

        private static NullReferenceException NullPtrException()
            => new NullReferenceException("Null pointer detected");

        /// <summary>
        /// Unboxes structure from unmanaged heap.
        /// </summary>
        /// <returns>Unboxed type.</returns>
        /// <exception cref="NullReferenceException">Attempt to dereference null pointer.</exception>
        public T Unbox() => pointer == Memory.NullPtr ? throw NullPtrException() : Memory.Dereference<T>(pointer);

        /// <summary>
        /// Creates a copy of value in the managed heap.
        /// </summary>
        /// <returns>A boxed copy in the managed heap.</returns>
        public Box<T> CopyToManagedHeap() => new Box<T>(Unbox());

        public OffHeapBuffer<T> Copy()
        {
            if(pointer == Memory.NullPtr)
                return this;
            var result = AllocUnitialized();
            Unsafe.CopyBlockUnaligned(result.pointer, pointer, (uint)Size);
            return result;
        }

        object ICloneable.Clone() => Copy();

        public static implicit operator IntPtr(OffHeapBuffer<T> heap) => new IntPtr(heap.pointer);

        [CLSCompliant(false)]
        public static implicit operator UIntPtr(OffHeapBuffer<T> heap) => new UIntPtr(heap.pointer);

        public static implicit operator ReadOnlySpan<T>(OffHeapBuffer<T> heap)
            => heap.pointer == Memory.NullPtr ? throw NullPtrException() : new ReadOnlySpan<T>(heap.pointer, 1);

        public static implicit operator T(OffHeapBuffer<T> heap) => heap.Unbox();

        /// <summary>
        /// Releases unmanaged memory associated with the boxed type.
        /// </summary>
        public void Dispose()
        {
            var pointer = new IntPtr(this.pointer);
            if(pointer != IntPtr.Zero)
                FreeHGlobal(pointer);
        }

        public bool Equals(OffHeapBuffer<T> other) => pointer == other.pointer;

        public override int GetHashCode() => new IntPtr(pointer).GetHashCode();

        public override bool Equals(object other)
        {
            switch(other)
            {
                case IntPtr pointer:
                    return new IntPtr(this.pointer) == pointer;
                case UIntPtr pointer:
                    return new UIntPtr(this.pointer) == pointer;
                case OffHeapBuffer<T> box:
                    return Equals(box);
                default:
                    return false;
            }
        }

        public bool Equals(T other, IEqualityComparer<T> comparer)
            => pointer != Memory.NullPtr && comparer.Equals(Memory.Dereference<T>(pointer), other);

        public int GetHashCode(IEqualityComparer<T> comparer)
            => pointer == Memory.NullPtr ? 0 : comparer.GetHashCode(Memory.Dereference<T>(pointer)); 

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(OffHeapBuffer<T> first, OffHeapBuffer<T> second) => first.pointer == second.pointer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)] 
        public static bool operator !=(OffHeapBuffer<T> first, OffHeapBuffer<T> second) => first.pointer != second.pointer;
    }
}