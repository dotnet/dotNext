using System.Buffers;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.IO
{
    using Buffers;
    using static Buffers.BufferReader;
    using static Pipelines.PipeExtensions;
    using DecodingContext = Text.DecodingContext;

    /// <summary>
    /// Represents binary reader for the sequence of bytes.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public struct SequenceReader : IAsyncBinaryReader
    {
        private readonly ReadOnlySequence<byte> sequence;
        private SequencePosition position;

        internal SequenceReader(ReadOnlySequence<byte> sequence)
        {
            this.sequence = sequence;
            position = sequence.Start;
        }

        internal SequenceReader(ReadOnlyMemory<byte> memory)
            : this(new ReadOnlySequence<byte>(memory))
        {
        }

        /// <summary>
        /// Resets the reader so it can be used again.
        /// </summary>
        public void Reset() => position = sequence.Start;

        /// <summary>
        /// Gets unread part of the sequence.
        /// </summary>
        public ReadOnlySequence<byte> RemainingSequence => sequence.Slice(position);

        /// <summary>
        /// Gets position in the underlying sequence.
        /// </summary>
        public SequencePosition Position => position;

        private TResult Read<TResult, TParser>(TParser parser)
            where TParser : struct, IBufferReader<TResult>
        {
            parser.Append<TResult, TParser>(RemainingSequence, out position);
            return parser.RemainingBytes == 0 ? parser.Complete() : throw new EndOfStreamException();
        }

        private TResult Parse<TResult, TBuffer>(Parser<TResult> parser, in DecodingContext context, TBuffer buffer, IFormatProvider? provider)
            where TResult : notnull
            where TBuffer : struct, IBuffer<char>
        {
            var reader = new StringReader<TBuffer>(in context, buffer);
            reader.Append<string, StringReader<TBuffer>>(RemainingSequence, out position);
            return reader.RemainingBytes == 0 ? parser(reader.Complete(), provider) : throw new EndOfStreamException();
        }

        /// <summary>
        /// Parses the value encoded as a set of characters.
        /// </summary>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <param name="parser">The parser.</param>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <param name="provider">The format provider.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        /// <exception cref="FormatException">The string is in wrong format.</exception>
        [SkipLocalsInit]
        public unsafe T Parse<T>(Parser<T> parser, LengthFormat lengthFormat, in DecodingContext context, IFormatProvider? provider = null)
            where T : notnull
        {
            var length = ReadLength(lengthFormat);
            if ((uint)length > MemoryRental<char>.StackallocThreshold)
            {
                using var buffer = new ArrayBuffer<char>(length);
                return Parse<T, ArrayBuffer<char>>(parser, in context, buffer, provider);
            }
            else
            {
                var buffer = stackalloc char[length];
                return Parse<T, UnsafeBuffer<char>>(parser, in context, new UnsafeBuffer<char>(buffer, length), provider);
            }
        }

        /// <summary>
        /// Decodes the value of blittable type from the sequence of bytes.
        /// </summary>
        /// <typeparam name="T">The type of the value to decode.</typeparam>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of sequence.</exception>
        public T Read<T>()
            where T : unmanaged
        {
            var result = default(T);
            Read(Span.AsBytes(ref result));
            return result;
        }

        /// <summary>
        /// Copies the bytes from the sequence into contiguous block of memory.
        /// </summary>
        /// <param name="output">The block of memory to fill.</param>
        /// <exception cref="EndOfStreamException">Unexpected end of sequence.</exception>
        public void Read(Span<byte> output)
        {
            RemainingSequence.CopyTo(output, out var writtenCount);
            if (writtenCount != output.Length)
                throw new EndOfStreamException();

            position = sequence.GetPosition(writtenCount, position);
        }

        /// <summary>
        /// Skips the specified number of bytes.
        /// </summary>
        /// <param name="length">The number of bytes to skip.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
        /// <exception cref="EndOfStreamException">Unexpected end of sequence.</exception>
        public void Skip(int length)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            try
            {
                position = sequence.GetPosition(length, position);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new EndOfStreamException(e.Message, e);
            }
        }

        /// <summary>
        /// Reads length-prefixed block of bytes.
        /// </summary>
        /// <param name="lengthFormat">The format of the block length encoded in the underlying stream.</param>
        /// <param name="allocator">The memory allocator used to place the decoded block of bytes.</param>
        /// <returns>The decoded block of bytes.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of sequence.</exception>
        public MemoryOwner<byte> Read(LengthFormat lengthFormat, MemoryAllocator<byte>? allocator = null)
        {
            var length = ReadLength(lengthFormat);
            MemoryOwner<byte> result;
            if (length > 0)
            {
                result = allocator.Invoke(length, true);
                Read(result.Memory.Span);
            }
            else
            {
                result = default;
            }

            return result;
        }

        /// <summary>
        /// Decodes 64-bit signed integer using the specified endianness.
        /// </summary>
        /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">The underlying sequence doesn't contain necessary amount of bytes to decode the value.</exception>
        public long ReadInt64(bool littleEndian)
        {
            var result = Read<long>();
            result.ReverseIfNeeded(littleEndian);
            return result;
        }

        /// <summary>
        /// Decodes 64-bit unsigned integer using the specified endianness.
        /// </summary>
        /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">The underlying sequence doesn't contain necessary amount of bytes to decode the value.</exception>
        [CLSCompliant(false)]
        public ulong ReadUInt64(bool littleEndian)
        {
            var result = Read<ulong>();
            result.ReverseIfNeeded(littleEndian);
            return result;
        }

        /// <summary>
        /// Decodes 32-bit signed integer using the specified endianness.
        /// </summary>
        /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">The underlying sequence doesn't contain necessary amount of bytes to decode the value.</exception>
        public int ReadInt32(bool littleEndian)
        {
            var result = Read<int>();
            result.ReverseIfNeeded(littleEndian);
            return result;
        }

        /// <summary>
        /// Decodes 32-bit unsigned integer using the specified endianness.
        /// </summary>
        /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">The underlying sequence doesn't contain necessary amount of bytes to decode the value.</exception>
        [CLSCompliant(false)]
        public uint ReadUInt32(bool littleEndian)
        {
            var result = Read<uint>();
            result.ReverseIfNeeded(littleEndian);
            return result;
        }

        /// <summary>
        /// Decodes 16-bit signed integer using the specified endianness.
        /// </summary>
        /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">The underlying sequence doesn't contain necessary amount of bytes to decode the value.</exception>
        public short ReadInt16(bool littleEndian)
        {
            var result = Read<short>();
            result.ReverseIfNeeded(littleEndian);
            return result;
        }

        /// <summary>
        /// Decodes 16-bit unsigned integer using the specified endianness.
        /// </summary>
        /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">The underlying sequence doesn't contain necessary amount of bytes to decode the value.</exception>
        [CLSCompliant(false)]
        public ushort ReadUInt16(bool littleEndian)
        {
            var result = Read<ushort>();
            result.ReverseIfNeeded(littleEndian);
            return result;
        }

        /// <summary>
        /// Decodes an arbitrary large integer.
        /// </summary>
        /// <param name="length">The length of the value, in bytes.</param>
        /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        [SkipLocalsInit]
        public unsafe BigInteger ReadBigInteger(int length, bool littleEndian)
        {
            BigInteger result;

            if (length == 0)
            {
                result = BigInteger.Zero;
            }
            else if ((uint)length > MemoryRental<byte>.StackallocThreshold)
            {
                using var buffer = new ArrayBuffer<byte>(length);
                result = Read<BigInteger, BigIntegerReader<ArrayBuffer<byte>>>(new BigIntegerReader<ArrayBuffer<byte>>(buffer, littleEndian));
            }
            else
            {
                var buffer = stackalloc byte[length];
                result = Read<BigInteger, BigIntegerReader<UnsafeBuffer<byte>>>(new BigIntegerReader<UnsafeBuffer<byte>>(new UnsafeBuffer<byte>(buffer, length), littleEndian));
            }

            return result;
        }

        /// <summary>
        /// Decodes an arbitrary large integer.
        /// </summary>
        /// <param name="lengthFormat">The format of the value length encoded in the underlying stream.</param>
        /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public BigInteger ReadBigInteger(LengthFormat lengthFormat, bool littleEndian)
            => ReadBigInteger(ReadLength(lengthFormat), littleEndian);

        /// <summary>
        /// Decodes the string.
        /// </summary>
        /// <param name="length">The length of the encoded string, in bytes.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <returns>The decoded string.</returns>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        [SkipLocalsInit]
        public unsafe string ReadString(int length, in DecodingContext context)
        {
            string result;

            if (length == 0)
            {
                result = string.Empty;
            }
            else if ((uint)length > MemoryRental<char>.StackallocThreshold)
            {
                using var buffer = new ArrayBuffer<char>(length);
                result = Read<string, StringReader<ArrayBuffer<char>>>(new StringReader<ArrayBuffer<char>>(in context, buffer));
            }
            else
            {
                var buffer = stackalloc char[length];
                result = Read<string, StringReader<UnsafeBuffer<char>>>(new StringReader<UnsafeBuffer<char>>(in context, new UnsafeBuffer<char>(buffer, length)));
            }

            return result;
        }

        private int ReadLength(LengthFormat lengthFormat)
        {
            int length;
            var littleEndian = BitConverter.IsLittleEndian;
            switch (lengthFormat)
            {
                default:
                    throw new ArgumentOutOfRangeException(nameof(lengthFormat));
                case LengthFormat.Plain:
                    length = Read<int>();
                    break;
                case LengthFormat.PlainLittleEndian:
                    littleEndian = true;
                    goto case LengthFormat.Plain;
                case LengthFormat.PlainBigEndian:
                    littleEndian = false;
                    goto case LengthFormat.Plain;
                case LengthFormat.Compressed:
                    length = Read<int, SevenBitEncodedIntReader>(new SevenBitEncodedIntReader(5));
                    break;
            }

            length.ReverseIfNeeded(littleEndian);
            return length;
        }

        /// <summary>
        /// Decodes the string.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <returns>The decoded string.</returns>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public string ReadString(LengthFormat lengthFormat, in DecodingContext context)
            => ReadString(ReadLength(lengthFormat), in context);

        /// <inheritdoc/>
        ValueTask<T> IAsyncBinaryReader.ReadAsync<T>(CancellationToken token)
        {
            ValueTask<T> result;

            if (token.IsCancellationRequested)
            {
                result = ValueTask.FromCanceled<T>(token);
            }
            else
            {
                try
                {
                    result = new(Read<T>());
                }
                catch (Exception e)
                {
                    result = ValueTask.FromException<T>(e);
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask IAsyncBinaryReader.ReadAsync(Memory<byte> output, CancellationToken token)
        {
            ValueTask result;

            if (token.IsCancellationRequested)
            {
                result = ValueTask.FromCanceled(token);
            }
            else
            {
                result = new ValueTask();
                try
                {
                    Read(output.Span);
                }
                catch (Exception e)
                {
                    result = ValueTask.FromException(e);
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask IAsyncBinaryReader.SkipAsync(int length, CancellationToken token)
        {
            ValueTask result;

            if (token.IsCancellationRequested)
            {
                result = ValueTask.FromCanceled(token);
            }
            else
            {
                result = new ValueTask();
                try
                {
                    Skip(length);
                }
                catch (Exception e)
                {
                    result = ValueTask.FromException(e);
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<MemoryOwner<byte>> IAsyncBinaryReader.ReadAsync(LengthFormat lengthFormat, MemoryAllocator<byte>? allocator, CancellationToken token)
        {
            ValueTask<MemoryOwner<byte>> result;

            if (token.IsCancellationRequested)
            {
                result = ValueTask.FromCanceled<MemoryOwner<byte>>(token);
            }
            else
            {
                try
                {
                    result = new(Read(lengthFormat, allocator));
                }
                catch (Exception e)
                {
                    result = ValueTask.FromException<MemoryOwner<byte>>(e);
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<long> IAsyncBinaryReader.ReadInt64Async(bool littleEndian, CancellationToken token)
        {
            ValueTask<long> result;
            if (token.IsCancellationRequested)
            {
                result = ValueTask.FromCanceled<long>(token);
            }
            else
            {
                try
                {
                    result = new(ReadInt64(littleEndian));
                }
                catch (Exception e)
                {
                    result = ValueTask.FromException<long>(e);
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<int> IAsyncBinaryReader.ReadInt32Async(bool littleEndian, CancellationToken token)
        {
            ValueTask<int> result;
            if (token.IsCancellationRequested)
            {
                result = ValueTask.FromCanceled<int>(token);
            }
            else
            {
                try
                {
                    result = new(ReadInt32(littleEndian));
                }
                catch (Exception e)
                {
                    result = ValueTask.FromException<int>(e);
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<short> IAsyncBinaryReader.ReadInt16Async(bool littleEndian, CancellationToken token)
        {
            ValueTask<short> result;
            if (token.IsCancellationRequested)
            {
                result = ValueTask.FromCanceled<short>(token);
            }
            else
            {
                try
                {
                    result = new(ReadInt16(littleEndian));
                }
                catch (Exception e)
                {
                    result = ValueTask.FromException<short>(e);
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<T> IAsyncBinaryReader.ParseAsync<T>(Parser<T> parser, LengthFormat lengthFormat, DecodingContext context, IFormatProvider? provider, CancellationToken token)
        {
            ValueTask<T> result;
            if (token.IsCancellationRequested)
            {
                result = ValueTask.FromCanceled<T>(token);
            }
            else
            {
                try
                {
                    result = new(Parse(parser, lengthFormat, in context, provider));
                }
                catch (Exception e)
                {
                    result = ValueTask.FromException<T>(e);
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<string> IAsyncBinaryReader.ReadStringAsync(int length, DecodingContext context, CancellationToken token)
        {
            ValueTask<string> result;
            if (token.IsCancellationRequested)
            {
                result = ValueTask.FromCanceled<string>(token);
            }
            else
            {
                try
                {
                    result = new(ReadString(length, context));
                }
                catch (Exception e)
                {
                    result = ValueTask.FromException<string>(e);
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<string> IAsyncBinaryReader.ReadStringAsync(LengthFormat lengthFormat, DecodingContext context, CancellationToken token)
        {
            ValueTask<string> result;
            if (token.IsCancellationRequested)
            {
                result = ValueTask.FromCanceled<string>(token);
            }
            else
            {
                try
                {
                    result = new(ReadString(lengthFormat, context));
                }
                catch (Exception e)
                {
                    result = ValueTask.FromException<string>(e);
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<BigInteger> IAsyncBinaryReader.ReadBigIntegerAsync(int length, bool littleEndian, CancellationToken token)
        {
            ValueTask<BigInteger> result;
            if (token.IsCancellationRequested)
            {
                result = ValueTask.FromCanceled<BigInteger>(token);
            }
            else
            {
                try
                {
                    result = new(ReadBigInteger(length, littleEndian));
                }
                catch (Exception e)
                {
                    result = ValueTask.FromException<BigInteger>(e);
                }
            }

            return result;
        }

        /// <inheritdoc/>
        ValueTask<BigInteger> IAsyncBinaryReader.ReadBigIntegerAsync(LengthFormat lengthFormat, bool littleEndian, CancellationToken token)
        {
            ValueTask<BigInteger> result;
            if (token.IsCancellationRequested)
            {
                result = ValueTask.FromCanceled<BigInteger>(token);
            }
            else
            {
                try
                {
                    result = new(ReadBigInteger(lengthFormat, littleEndian));
                }
                catch (Exception e)
                {
                    result = ValueTask.FromException<BigInteger>(e);
                }
            }

            return result;
        }

        /// <inheritdoc/>
        Task IAsyncBinaryReader.CopyToAsync(Stream output, CancellationToken token)
            => output.WriteAsync(RemainingSequence, token).AsTask();

        /// <inheritdoc/>
        Task IAsyncBinaryReader.CopyToAsync(PipeWriter output, CancellationToken token)
            => output.WriteAsync(RemainingSequence, token).AsTask();

        /// <inheritdoc/>
        Task IAsyncBinaryReader.CopyToAsync(IBufferWriter<byte> writer, CancellationToken token)
        {
            Task result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled(token);
            }
            else
            {
                result = Task.CompletedTask;
                try
                {
                    writer.Write(RemainingSequence);
                }
                catch (Exception e)
                {
                    result = Task.FromException(e);
                }
            }

            return result;
        }

        /// <inheritdoc/>
        Task IAsyncBinaryReader.CopyToAsync<TArg>(ReadOnlySpanAction<byte, TArg> reader, TArg arg, CancellationToken token)
        {
            Task result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled(token);
            }
            else
            {
                result = Task.CompletedTask;
                try
                {
                    for (ReadOnlyMemory<byte> block; sequence.TryGet(ref position, out block); token.ThrowIfCancellationRequested())
                        reader(block.Span, arg);
                }
                catch (Exception e)
                {
                    result = Task.FromException(e);
                }
            }

            return result;
        }

        /// <inheritdoc/>
        async Task IAsyncBinaryReader.CopyToAsync<TArg>(Func<TArg, ReadOnlyMemory<byte>, CancellationToken, ValueTask> reader, TArg arg, CancellationToken token)
        {
            foreach (var segment in RemainingSequence)
                await reader(arg, segment, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        async Task IAsyncBinaryReader.CopyToAsync<TConsumer>(TConsumer consumer, CancellationToken token)
        {
            foreach (var segment in RemainingSequence)
                await consumer.Invoke(segment, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        bool IAsyncBinaryReader.TryGetSequence(out ReadOnlySequence<byte> bytes)
        {
            bytes = RemainingSequence;
            return true;
        }
    }
}