using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Missing = System.Reflection.Missing;

namespace DotNext.IO
{
    using Buffers;
    using static Pipelines.ResultExtensions;
    using DecodingContext = Text.DecodingContext;

    /// <summary>
    /// Represents binary reader for the sequence of bytes.
    /// </summary>
    public struct SequenceBinaryReader : IAsyncBinaryReader
    {
        private readonly ReadOnlySequence<byte> sequence;
        private SequencePosition position;

        internal SequenceBinaryReader(ReadOnlySequence<byte> sequence)
        {
            this.sequence = sequence;
            position = sequence.Start;
        }

        /// <summary>
        /// Resets the reader so it can be used again.
        /// </summary>
        public void Reset() => position = sequence.Start;

        private TResult Read<TResult, TParser>(TParser parser)
            where TParser : struct, IBufferReader<TResult>
        {
            parser.Append<TResult, TParser>(sequence.Slice(position), out position);
            return parser.RemainingBytes == 0 ? parser.Complete() : throw new EndOfStreamException();
        }

        /// <summary>
        /// Decodes the value of blittable type from the sequence of bytes.
        /// </summary>
        /// <typeparam name="T">The type of the value to decode.</typeparam>
        /// <returns>The decoded value.</returns>
        /// <exception cref="EndOfStreamException">Unexpected end of sequence.</exception>
        public T Read<T>()
            where T : unmanaged => Read<T, ValueReader<T>>(new ValueReader<T>());

        /// <summary>
        /// Copies the bytes from the sequence into contiguous block of memory.
        /// </summary>
        /// <param name="output">The block of memory to fill.</param>
        /// <exception cref="EndOfStreamException">Unexpected end of sequence.</exception>
        public void Read(Memory<byte> output) => Read<Missing, MemoryReader>(new MemoryReader(output));

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
        /// Decodes the string.
        /// </summary>
        /// <param name="length">The length of the encoded string, in bytes.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <returns>The decoded string.</returns>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public unsafe string ReadString(int length, in DecodingContext context)
        {
            if (length > MemoryRental<char>.StackallocThreshold)
            {
                var buffer = new ArrayBuffer<char>(length);
                return Read<string, StringReader<ArrayBuffer<char>>>(new StringReader<ArrayBuffer<char>>(in context, buffer));
            }
            else
            {
                var buffer = stackalloc char[length];
                return Read<string, StringReader<UnsafeBuffer<char>>>(new StringReader<UnsafeBuffer<char>>(in context, new UnsafeBuffer<char>(buffer, length)));
            }
        }

        /// <summary>
        /// Decodes the string.
        /// </summary>
        /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
        /// <param name="context">The decoding context containing string characters encoding.</param>
        /// <returns>The decoded string.</returns>
        /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
        public string ReadString(StringLengthEncoding lengthFormat, in DecodingContext context)
        {
            int length;
            var littleEndian = BitConverter.IsLittleEndian;
            switch (lengthFormat)
            {
                default:
                    throw new ArgumentOutOfRangeException(nameof(lengthFormat));
                case StringLengthEncoding.Plain:
                    length = Read<int>();
                    break;
                case StringLengthEncoding.PlainLittleEndian:
                    littleEndian = true;
                    goto case StringLengthEncoding.Plain;
                case StringLengthEncoding.PlainBigEndian:
                    littleEndian = false;
                    goto case StringLengthEncoding.Plain;
                case StringLengthEncoding.Compressed:
                    length = Read<int, SevenBitEncodedIntReader>(new SevenBitEncodedIntReader(5));
                    break;
            }

            length.ReverseIfNeeded(littleEndian);
            return ReadString(length, in context);
        }

        /// <inheritdoc/>
        ValueTask<T> IAsyncBinaryReader.ReadAsync<T>(CancellationToken token)
            => token.IsCancellationRequested ?
                new ValueTask<T>(Task.FromCanceled<T>(token)) :
                new ValueTask<T>(Read<T>());

        /// <inheritdoc/>
        ValueTask IAsyncBinaryReader.ReadAsync(Memory<byte> output, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return new ValueTask(Task.FromCanceled(token));
            Read(output);
            return new ValueTask();
        }

        /// <inheritdoc/>
        ValueTask<long> IAsyncBinaryReader.ReadInt64Async(bool littleEndian, CancellationToken token)
            => token.IsCancellationRequested ?
                new ValueTask<long>(Task.FromCanceled<long>(token)) :
                new ValueTask<long>(ReadInt64(littleEndian));

        /// <inheritdoc/>
        ValueTask<int> IAsyncBinaryReader.ReadInt32Async(bool littleEndian, CancellationToken token)
            => token.IsCancellationRequested ?
                new ValueTask<int>(Task.FromCanceled<int>(token)) :
                new ValueTask<int>(ReadInt32(littleEndian));

        /// <inheritdoc/>
        ValueTask<short> IAsyncBinaryReader.ReadInt16Async(bool littleEndian, CancellationToken token)
            => token.IsCancellationRequested ?
                new ValueTask<short>(Task.FromCanceled<short>(token)) :
                new ValueTask<short>(ReadInt16(littleEndian));

        /// <inheritdoc/>
        ValueTask<string> IAsyncBinaryReader.ReadStringAsync(int length, DecodingContext context, CancellationToken token)
            => token.IsCancellationRequested ?
                new ValueTask<string>(Task.FromCanceled<string>(token)) :
                new ValueTask<string>(ReadString(length, in context));

        /// <inheritdoc/>
        ValueTask<string> IAsyncBinaryReader.ReadStringAsync(StringLengthEncoding lengthFormat, DecodingContext context, CancellationToken token)
            => token.IsCancellationRequested ?
                new ValueTask<string>(Task.FromCanceled<string>(token)) :
                new ValueTask<string>(ReadString(lengthFormat, in context));

        /// <inheritdoc/>
        async Task IAsyncBinaryReader.CopyToAsync(Stream output, CancellationToken token)
        {
            while (sequence.TryGet(ref position, out var block))
                await output.WriteAsync(block, token).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        async Task IAsyncBinaryReader.CopyToAsync(PipeWriter output, CancellationToken token)
        {
            while (sequence.TryGet(ref position, out var block))
            {
                var result = await output.WriteAsync(block, token).ConfigureAwait(false);
                result.ThrowIfCancellationRequested();
                if (result.IsCompleted)
                    break;
            }
        }
    }
}