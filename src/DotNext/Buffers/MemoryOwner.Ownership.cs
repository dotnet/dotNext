using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

partial struct MemoryOwner<T>
{
    internal MemoryOwner(in Ownership ownership, int length)
    {
        Debug.Assert(ownership.Length >= length);

        this.ownership = ownership;
        this.length = length;
    }

    internal readonly Ownership TakeOwnership() => ownership;
    
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct Ownership
    {
        // Of type ArrayPool<T> or IMemoryOwner<T>.
        // If support of another type is needed then reconsider implementation
        // of Memory and this[int index] members
        private readonly object? owner;
        private readonly T[]? array;  // not null only if owner is ArrayPool or null

        public Ownership(ArrayPool<T>? owner, T[] array)
        {
            Debug.Assert(array is { Length: > 0 });
            
            this.owner = owner;
            this.array = array;
        }

        public Ownership(IMemoryOwner<T> owner)
            => this.owner = owner;

        public Span<T> Span
        {
            get
            {
                Span<T> result = array;
                if (result.IsEmpty && owner is not null)
                    result = Unsafe.As<IMemoryOwner<T>>(owner).Memory.Span;

                return result;
            }
        }

        public Memory<T> Memory => array?.AsMemory() ?? Unsafe.As<IMemoryOwner<T>>(owner)?.Memory ?? Memory<T>.Empty;
        
        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => array?.Length ?? Unsafe.As<IMemoryOwner<T>>(owner)?.Memory.Length ?? 0;
        }

        public bool IsEmpty => owner is null && array is null;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public ref T DataRef
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref array is not null
                ? ref MemoryMarshal.GetArrayDataReference(array)
                : ref owner is not null
                    ? ref MemoryMarshal.GetReference(Unsafe.As<IMemoryOwner<T>>(owner).Memory.Span)
                    : ref Unsafe.NullRef<T>();
        }

        [Conditional("DEBUG")]
        public void AssertState(int length)
        {
            Debug.Assert(length is 0 ^ (array is not null || owner is not null));
            Debug.Assert(owner is null or ArrayPool<T> or IMemoryOwner<T>);
            Debug.Assert(array is null or { Length: > 0 });
            Debug.Assert(array is null ? owner is null or IMemoryOwner<T> : owner is null or ArrayPool<T>);
        }
        
        public void Clear(bool clearBuffer)
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
    }
}