using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers
{
    /// <summary>
    /// Represents stack-allocated buffer builder.
    /// </summary>
    /// <remarks>
    /// This type is similar to <see cref="PooledArrayBufferWriter{T}"/> and <see cref="PooledBufferWriter{T}"/>
    /// classes but it tries to avoid on-heap allocation. Moreover, it can use pre-allocated stack
    /// memory as a initial buffer used for writing. If builder requires more space then pooled
    /// memory used.
    /// </remarks>
    /// <typeparam name="T">The type of the elements in the memory.</typeparam>
    /// <seealso cref="PooledArrayBufferWriter{T}"/>
    /// <seealso cref="PooledBufferWriter{T}"/>
    [StructLayout(LayoutKind.Auto)]
    [DebuggerDisplay("WrittenCount = {" + nameof(WrittenCount) + "}, FreeCapacity = {" + nameof(FreeCapacity) + "}, Overflow = {" + nameof(Overflow) + "}")]
    public ref struct BufferWriterSlim<T>
    {
        private readonly Span<T> initialBuffer;
        private readonly MemoryAllocator<T>? allocator;
        private MemoryOwner<T> extraBuffer;
        private int position;

        /// <summary>
        /// Initializes growable builder.
        /// </summary>
        /// <param name="buffer">Pre-allocated buffer used by this builder.</param>
        /// <param name="allocator">The memory allocator used to rent the memory blocks.</param>
        /// <remarks>
        /// The builder created with this constructor is growable. However, additional memory will not be
        /// requested using <paramref name="allocator"/> while <paramref name="buffer"/> space is sufficient.
        /// If <paramref name="allocator"/> is <see langword="null"/> then <see cref="ArrayPool{T}.Shared"/>
        /// is used for memory pooling.
        /// </remarks>
        public BufferWriterSlim(Span<T> buffer, MemoryAllocator<T>? allocator = null)
        {
            initialBuffer = buffer;
            this.allocator = allocator;
            extraBuffer = default;
            position = 0;
        }

        private int Overflow => Math.Max(0, position - initialBuffer.Length);

        /// <summary>
        /// Gets the amount of data written to the underlying memory so far.
        /// </summary>
        public readonly int WrittenCount => position;

        /// <summary>
        /// Gets the total amount of space within the underlying memory.
        /// </summary>
        public readonly int Capacity => initialBuffer.Length + extraBuffer.Length;

        /// <summary>
        /// Gets the amount of space available that can still be written into without forcing the underlying buffer to grow.
        /// </summary>
        public readonly int FreeCapacity => Capacity - WrittenCount;

        /// <summary>
        /// Gets span over constructed memory block.
        /// </summary>
        /// <value>The constructed memory block.</value>
        public readonly ReadOnlySpan<T> WrittenSpan
        {
            get
            {
                var result = position <= initialBuffer.Length ? initialBuffer : extraBuffer.Memory.Span;
                return result.Slice(0, position);
            }
        }

        /// <summary>
        /// Writes elements to this buffer.
        /// </summary>
        /// <param name="input">The span of elements to be written.</param>
        /// <exception cref="InsufficientMemoryException">Pre-allocated initial buffer size is not enough to place <paramref name="input"/> elements to it and this builder is not growable.</exception>
        /// <exception cref="OverflowException">The size of the internal buffer becomes greater than <see cref="int.MaxValue"/>.</exception>
        public void Write(ReadOnlySpan<T> input)
        {
            if (input.IsEmpty)
                return;

            var newSize = checked(position + input.Length);
            Span<T> output;
            int offset;

            // grow if needed
            if (newSize > initialBuffer.Length)
            {
                if (extraBuffer.IsEmpty)
                {
                    extraBuffer = allocator.Invoke(newSize, false);
                    initialBuffer.CopyTo(extraBuffer.Memory.Span);
                    initialBuffer.Clear();
                }
                else if (newSize > extraBuffer.Length)
                {
                    var newBuffer = allocator.Invoke(newSize, false);
                    extraBuffer.Memory.CopyTo(newBuffer.Memory);
                    extraBuffer.Dispose();
                    extraBuffer = newBuffer;
                }

                output = extraBuffer.Memory.Span;
            }
            else
            {
                output = initialBuffer;
            }

            offset = position;

            input.CopyTo(output.Slice(offset));
            position = newSize;
        }

        /// <summary>
        /// Adds single element to this builder.
        /// </summary>
        /// <param name="item">The item to be added.</param>
        /// <exception cref="InsufficientMemoryException">Pre-allocated initial buffer size is not enough to place <paramref name="item"/> to it and this builder is not growable.</exception>
        public void Add(T item) => Write(MemoryMarshal.CreateReadOnlySpan(ref item, 1));

        /// <summary>
        /// Gets the element at the specified zero-based index within this builder.
        /// </summary>
        /// <param name="index">he zero-based index of the element.</param>
        /// <value>The element at the specified index.</value>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than zero or greater than or equal to <see cref="WrittenCount"/>.</exception>
        public ref T this[int index]
        {
            get
            {
                if (index < 0 || index >= position)
                    throw new ArgumentOutOfRangeException(nameof(index));

                var buffer = position <= initialBuffer.Length ? initialBuffer : extraBuffer.Memory.Span;
                return ref Unsafe.Add(ref MemoryMarshal.GetReference(buffer), index);
            }
        }

        /// <summary>
        /// lears the data written to the underlying buffer.
        /// </summary>
        /// <param name="reuseBuffer"><see langword="true"/> to reuse the internal buffer; <see langword="false"/> to destroy the internal buffer.</param>
        public void Clear(bool reuseBuffer)
        {
            initialBuffer.Clear();
            if (!reuseBuffer)
            {
                extraBuffer.Dispose();
                extraBuffer = default;
            }
            else if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                extraBuffer.Memory.Span.Clear();
            }

            position = 0;
        }

        /// <summary>
        /// Releases internal buffer used by this builder.
        /// </summary>
        public void Dispose()
        {
            extraBuffer.Dispose();
            this = default;
        }
    }
}