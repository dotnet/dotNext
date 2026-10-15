using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext.Buffers;

public static partial class Memory
{
    /// <summary>
    /// Extends <see cref="ArrayPool{T}"/> type.
    /// </summary>
    /// <param name="pool">The array pool.</param>
    /// <typeparam name="T">The type of the items in the memory pool.</typeparam>
    extension<T>(ArrayPool<T> pool)
    {
        private MemoryOwner<T> Allocate(int length)
            => new(pool, length, exactSize: false);

        /// <summary>
        /// Converts array pool to the memory allocator.
        /// </summary>
        /// <returns>The array allocator.</returns>
        public MemoryAllocator<T> ToAllocator()
            => pool.Allocate;
    }

    /// <summary>
    /// Extends <see cref="MemoryPool{T}"/> type.
    /// </summary>
    /// <param name="pool">The memory pool.</param>
    /// <typeparam name="T">The type of the items in the memory pool.</typeparam>
    extension<T>(MemoryPool<T> pool)
    {
        private MemoryOwner<T> Allocate(int length)
            => new(pool, length);

        /// <summary>
        /// Converts memory pool to the memory allocator.
        /// </summary>
        /// <returns>The memory allocator.</returns>
        public MemoryAllocator<T> ToAllocator()
            => pool.Allocate;
    }

    /// <summary>
    /// Extends <see cref="Func{Int32, IMemoryOwner}"/> type.
    /// </summary>
    /// <param name="provider">The memory provider.</param>
    /// <typeparam name="T">The type of the items in the memory pool.</typeparam>
    extension<T>(Func<int, IMemoryOwner<T>> provider)
    {
        private MemoryOwner<T> Allocate(int length)
            => new(provider, length);

        /// <summary>
        /// Converts memory provider to the memory allocator.
        /// </summary>
        /// <returns>The memory allocator.</returns>
        public MemoryAllocator<T> ToAllocator()
            => provider.Allocate;
    }

    /// <param name="allocator">The memory allocator.</param>
    extension<T>(MemoryAllocator<T> allocator)
    {
        /// <summary>
        /// Allocates memory of at least <paramref name="length"/> size.
        /// </summary>
        /// <param name="length">The number of items in the rented memory.</param>
        /// <returns>The allocated memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MemoryOwner<T> AllocateAtLeast(int length)
            => allocator.Invoke(length);

        /// <summary>
        /// Allocates memory of <paramref name="length"/> size.
        /// </summary>
        /// <param name="length">The number of items in the rented memory.</param>
        /// <returns>The allocated memory.</returns>]
        public MemoryOwner<T> AllocateExactly(int length)
        {
            var result = allocator(length);
            result.Truncate(length);
            return result;
        }
        
        /// <summary>
        /// Gets the array allocator.
        /// </summary>
        /// <value>The array allocator.</value>
        public static MemoryAllocator<T> Array
        {
            get
            {
                return AllocateArray;

                static MemoryOwner<T> AllocateArray(int length)
                    => new(GC.AllocateUninitializedArray<T>(length, pinned: false));
            }
        }

        /// <summary>
        /// Gets the allocator, which is <see cref="Memory.get_ArrayPool{T}"/> by default.
        /// </summary>
        /// <remarks>
        /// The default allocator can be changed to <see cref="get_Array{T}"/> by
        /// turning off <c>DotNext.Buffers.MemoryAllocator.Default.UseArrayPool</c> switch.
        /// </remarks>
        public static MemoryAllocator<T> Default
            => Features.UseArrayPoolAsDefaultAllocator ? get_ArrayPool<T>() : get_Array<T>();

        /// <summary>
        /// Gets the allocator which uses <see cref="ArrayPool{T}.Shared"/> pool.
        /// </summary>
        public static MemoryAllocator<T> ArrayPool
        {
            get
            {
                return AllocateUsingArrayPool;

                static MemoryOwner<T> AllocateUsingArrayPool(int length)
                    => new(ArrayPool<T>.Shared, length, exactSize: false);
            }
        }

        /// <summary>
        /// Gets the allocator that rents memory from <see cref="ArrayPool{T}.Shared">ArrayPool&lt;byte&gt;.Shared</see>
        /// and reinterprets it as elements of type <typeparamref name="T"/>.
        /// </summary>
        /// <remarks>
        /// All element types share a single pool of byte arrays instead of keeping a separate pool per type.
        /// This applies to types that contain no references and require alignment no larger than the pointer size,
        /// such as <see cref="char"/>, <see cref="int"/>, <see cref="double"/>, or structs composed of them.
        /// For other types, as well as for small or very large buffers, the allocator falls back
        /// to <see cref="ArrayPool{T}.Shared"/>.
        /// <para>
        /// Memory rented from the byte pool is backed by <see cref="MemoryManager{T}"/> rather than an array:
        /// each rental allocates a small wrapper object, and <see cref="System.Runtime.InteropServices.MemoryMarshal.TryGetArray{T}(ReadOnlyMemory{T}, out ArraySegment{T})"/>
        /// returns <see langword="false"/> for such memory.
        /// </para>
        /// </remarks>
        public static MemoryAllocator<T> ByteArrayPool => ReinterpretedMemoryManager<T>.Rent;

        internal bool IsDefault => allocator.Equals(get_Default<T>());

        internal bool IsArrayPool => allocator.Equals(get_ArrayPool<T>());

        internal bool IsByteArrayPool => allocator.Equals(get_ByteArrayPool<T>());

        internal bool IsArray => allocator.Equals(get_Array<T>());

        internal static MemoryAllocator<T> Throwable
        {
            get
            {
                return Throw;

                static MemoryOwner<T> Throw(int size) => throw new BufferSizeLimitExceededException();
            }
        }
    }

    /// <summary>
    /// Extends <see cref="MemoryAllocator{T}"/> delegate.
    /// </summary>
    /// <typeparam name="T">The type of the items in the memory pool.</typeparam>
    extension<T>(MemoryAllocator<T>? allocator)
    {
        /// <summary>
        /// Gets <see cref="Memory.get_Default{T}"/> allocator if the current is <see langword="null"/>.
        /// </summary>
        public MemoryAllocator<T> DefaultIfNull => allocator ?? get_Default<T>();
    }

    /// <summary>
    /// Extends <see cref="MemoryAllocator{T}"/> with static members.
    /// </summary>
    /// <typeparam name="T">The blittable type.</typeparam>
    extension<T>(MemoryAllocator<T>) where T : unmanaged
    {
        /// <summary>
        /// Returns an allocator of pinned arrays.
        /// </summary>
        /// <value>The array allocator.</value>
        public static MemoryAllocator<T> Pinned
        {
            get
            {
                return AllocateArray;

                static MemoryOwner<T> AllocateArray(int length)
                    => new(GC.AllocateUninitializedArray<T>(length, pinned: true));
            }
        }
    }
}

file static class Features
{
    private const string DefaultAllocatorFeatureName = "DotNext.Buffers.MemoryAllocator.Default.UseArrayPool";

    [FeatureSwitchDefinition(DefaultAllocatorFeatureName)]
    public static bool UseArrayPoolAsDefaultAllocator { get; } = AppContext.IsFeatureSupported(DefaultAllocatorFeatureName);
}