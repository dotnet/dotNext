using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

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
        // TODO: Support of BinaryPrimitives should be added using function pointers in C# 9
        private readonly Span<T> initialBuffer;
        private readonly MemoryAllocator<T>? allocator;
        private readonly bool copyOnOverflow;
        private MemoryOwner<T> extraBuffer;
        private int position;

        /// <summary>
        /// Initializes growable builder.
        /// </summary>
        /// <param name="buffer">Pre-allocated buffer used by this builder.</param>
        /// <param name="copyOnOverflow">
        /// <see langword="true"/> to copy pre-allocated buffer to pooled memory on overflow;
        /// <see langword="false"/> to keep head of the written content in the pre-allocated buffer.
        /// </param>
        /// <param name="allocator">The memory allocator used to rent the memory blocks.</param>
        /// <remarks>
        /// The builder created with this constructor is growable. However, additional memory will not be
        /// requested using <paramref name="allocator"/> while <paramref name="buffer"/> space is sufficient.
        /// If <paramref name="allocator"/> is <see langword="null"/> then <see cref="ArrayPool{T}.Shared"/>
        /// is used for memory pooling.
        /// <see cref="WrittenSpan"/> property is supported only if <paramref name="copyOnOverflow"/> is <see langword="true"/>.
        /// Otherwise, it's not possible to represent written content as contiguous memory block.
        /// </remarks>
        public BufferWriterSlim(Span<T> buffer, bool copyOnOverflow, MemoryAllocator<T>? allocator = null)
        {
            initialBuffer = buffer;
            this.allocator = allocator;
            extraBuffer = default;
            position = 0;
            this.copyOnOverflow = copyOnOverflow;
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
        /// <exception cref="NotSupportedException">
        /// If this builder was constructed using <see cref="BufferWriterSlim{T}(Span{T}, bool, MemoryAllocator{T})"/>
        /// constructor and <c>copyOnOverflow</c> parameter is <see langword="false"/>.
        /// </exception>
        public readonly ReadOnlySpan<T> WrittenSpan
        {
            get
            {
                if (!copyOnOverflow)
                    throw new NotSupportedException();

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
            MemoryOwner<T> newBuffer;

            if (copyOnOverflow)
            {
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
                        newBuffer = allocator.Invoke(newSize, false);
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
            }
            else
            {
                // grow if needed
                if (newSize > initialBuffer.Length && newSize - initialBuffer.Length > extraBuffer.Length)
                {
                    newBuffer = allocator.Invoke(newSize, false);
                    extraBuffer.Memory.CopyTo(newBuffer.Memory);
                    extraBuffer.Dispose();
                    extraBuffer = newBuffer;
                }

                // append elements
                if (position < initialBuffer.Length)
                {
                    var writtenCount = Math.Min(initialBuffer.Length - position, input.Length);
                    input.Slice(0, writtenCount).CopyTo(initialBuffer.Slice(position));
                    input = input.Slice(writtenCount);
                    offset = 0;
                }
                else
                {
                    offset = position - initialBuffer.Length;
                }

                output = extraBuffer.Memory.Span;
            }

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
        public readonly ref T this[int index]
        {
            get
            {
                if (index < 0 || index >= position)
                    throw new ArgumentOutOfRangeException(nameof(index));

                Span<T> buffer;
                if (copyOnOverflow)
                {
                    buffer = position <= initialBuffer.Length ? initialBuffer : extraBuffer.Memory.Span;
                }
                else if (extraBuffer.IsEmpty || index < initialBuffer.Length)
                {
                    buffer = initialBuffer;
                }
                else
                {
                    index -= initialBuffer.Length;
                    buffer = extraBuffer.Memory.Span;
                }

                return ref buffer[index];
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
        /// Copies written content to the specified buffer writer.
        /// </summary>
        /// <param name="output">The buffer writer.</param>
        public readonly void CopyTo(IBufferWriter<T> output)
            => CopyTo(DotNext.Span.CopyTo, output); // TODO: Must be rewritten using function pointer

        private readonly void GetSegments(out ReadOnlySpan<T> head, out ReadOnlySpan<T> tail)
        {
            if (!copyOnOverflow)
            {
                var count = Math.Min(position, initialBuffer.Length);
                head = initialBuffer.Slice(0, count);
                tail = extraBuffer.Memory.Span;
                tail = tail.Slice(0, position - count);
            }
            else if (extraBuffer.IsEmpty)
            {
                head = initialBuffer.Slice(0, position);
                tail = default;
            }
            else
            {
                head = extraBuffer.Memory.Span;
                head = head.Slice(0, position);
                tail = default;
            }
        }

        /// <summary>
        /// Transfers written content to the specified callback.
        /// </summary>
        /// <typeparam name="TArg">The type of the argument to be passed to the callback.</typeparam>
        /// <param name="action">The callback accepting written content. Can be called more than once.</param>
        /// <param name="arg">The argument to be passed to the callback.</param>
        public readonly void CopyTo<TArg>(ReadOnlySpanAction<T, TArg> action, TArg arg)
        {
            GetSegments(out var head, out var tail);

            if (!head.IsEmpty)
            {
                action(head, arg);
                if (!tail.IsEmpty)
                {
                    action(tail, arg);
                }
            }
        }

        /// <summary>
        /// Copies written content.
        /// </summary>
        /// <param name="output">The memory writer.</param>
        /// <returns>The actual number of copied elements.</returns>
        public readonly int CopyTo(ref SpanWriter<T> output)
        {
            GetSegments(out var head, out var tail);
            int writtenCount;
            writtenCount = output.Write(head);
            writtenCount += output.Write(tail);
            return writtenCount;
        }

        /// <summary>
        /// Copies written content to the specified span.
        /// </summary>
        /// <remarks>
        /// The output span can be larger or smaller than <see cref="WrittenCount"/>.
        /// </remarks>
        /// <param name="output">The span of elemenents to modify.</param>
        /// <returns>The actual number of copied elements.</returns>
        public readonly int CopyTo(Span<T> output)
        {
            GetSegments(out var head, out var tail);
            head.CopyTo(output, out var count1);
            tail.CopyTo(output.Slice(count1), out var count2);
            return count1 + count2;
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

    /// <summary>
    /// Provides extension methods for <see cref="BufferWriterSlim{T}"/> type.
    /// </summary>
    public static class BufferWriterSlim
    {
        // TODO: Must be rewritten using function pointer

        /// <summary>
        /// Copies written bytes to the stream.
        /// </summary>
        /// <param name="builder">The buffer builder.</param>
        /// <param name="output">The output stream.</param>
        public static void CopyTo(this in BufferWriterSlim<byte> builder, Stream output)
            => builder.CopyTo(Span.CopyTo, output);

        /// <summary>
        /// Copies written characters to the text stream.
        /// </summary>
        /// <param name="builder">The buffer builder.</param>
        /// <param name="output">The output stream.</param>
        public static void CopyTo(this in BufferWriterSlim<char> builder, TextWriter output)
            => builder.CopyTo(Span.CopyTo, output);

        /// <summary>
        /// Copies written characters to string builder.
        /// </summary>
        /// <param name="builder">The buffer builder.</param>
        /// <param name="output">The string builder.</param>
        public static void CopyTo(this in BufferWriterSlim<char> builder, StringBuilder output)
            => builder.CopyTo(Span.CopyTo, output);
    }
}