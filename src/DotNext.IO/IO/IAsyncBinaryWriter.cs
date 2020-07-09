using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using static System.Globalization.CultureInfo;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.IO
{
    using static Buffers.BufferReader;
    using static Pipelines.ResultExtensions;
    using ByteBuffer = Buffers.ArrayRental<byte>;
    using EncodingContext = Text.EncodingContext;

    /// <summary>
    /// Providers a uniform way to encode the data.
    /// </summary>
    /// <seealso cref="IAsyncBinaryReader"/>
    public interface IAsyncBinaryWriter
    {
        /// <summary>
        /// Encodes value of blittable type.
        /// </summary>
        /// <param name="value">The value to encode.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <typeparam name="T">The type of the value to encode.</typeparam>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        async ValueTask WriteAsync<T>(T value, CancellationToken token = default)
            where T : unmanaged
        {
            using var buffer = new ByteBuffer(Unsafe.SizeOf<T>());
            Span.AsReadOnlyBytes(value).CopyTo(buffer.Span);
            await WriteAsync(buffer.Memory, token).ConfigureAwait(false);
        }

        private ValueTask WriteAsync<T>(T value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
            where T : struct, IFormattable
            => WriteAsync(value.ToString(format, provider).AsMemory(), context, lengthFormat, token);

        /// <summary>
        /// Encodes 64-bit signed integer asynchronously.
        /// </summary>
        /// <param name="value">The value to encode.</param>
        /// <param name="littleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        ValueTask WriteInt64Async(long value, bool littleEndian, CancellationToken token = default)
        {
            value.ReverseIfNeeded(littleEndian);
            return WriteAsync(value, token);
        }

        /// <summary>
        /// Encodes 64-bit signed integer as a string.
        /// </summary>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The context describing encoding of characters.</param>
        /// <param name="format">A standard or custom numeric format string.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        ValueTask WriteInt64Async(long value, StringLengthEncoding lengthFormat, EncodingContext context, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync(value, lengthFormat, context, format, provider, token);

        /// <summary>
        /// Encodes 32-bit signed integer asynchronously.
        /// </summary>
        /// <param name="value">The value to encode.</param>
        /// <param name="littleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        ValueTask WriteInt32Async(int value, bool littleEndian, CancellationToken token = default)
        {
            value.ReverseIfNeeded(littleEndian);
            return WriteAsync(value, token);
        }

        /// <summary>
        /// Encodes 32-bit signed integer as a string.
        /// </summary>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The context describing encoding of characters.</param>
        /// <param name="format">A standard or custom numeric format string.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        ValueTask WriteInt32Async(int value, StringLengthEncoding lengthFormat, EncodingContext context, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync(value, lengthFormat, context, format, provider, token);

        /// <summary>
        /// Encodes 16-bit signed integer asynchronously.
        /// </summary>
        /// <param name="value">The value to encode.</param>
        /// <param name="littleEndian"><see langword="true"/> to use little-endian encoding; <see langword="false"/> to use big-endian encoding.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        ValueTask WriteInt16Async(short value, bool littleEndian, CancellationToken token = default)
        {
            value.ReverseIfNeeded(littleEndian);
            return WriteAsync(value, token);
        }

        /// <summary>
        /// Encodes 16-bit signed integer as a string.
        /// </summary>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The context describing encoding of characters.</param>
        /// <param name="format">A standard or custom numeric format string.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        ValueTask WriteInt16Async(short value, StringLengthEncoding lengthFormat, EncodingContext context, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync(value, lengthFormat, context, format, provider, token);

        /// <summary>
        /// Encodes 8-bit unsigned integer as a string.
        /// </summary>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The context describing encoding of characters.</param>
        /// <param name="format">A standard or custom numeric format string.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        ValueTask WriteByteAsync(byte value, StringLengthEncoding lengthFormat, EncodingContext context, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync(value, lengthFormat, context, format, provider, token);

        /// <summary>
        /// Encodes <see cref="decimal"/> as a string.
        /// </summary>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The context describing encoding of characters.</param>
        /// <param name="format">A standard or custom numeric format string.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        ValueTask WriteDecimalAsync(decimal value, StringLengthEncoding lengthFormat, EncodingContext context, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync(value, lengthFormat, context, format, provider, token);

        /// <summary>
        /// Encodes single-precision floating-point number as a string.
        /// </summary>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The context describing encoding of characters.</param>
        /// <param name="format">A standard or custom numeric format string.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        ValueTask WriteSingleAsync(float value, StringLengthEncoding lengthFormat, EncodingContext context, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync(value, lengthFormat, context, format, provider, token);

        /// <summary>
        /// Encodes double-precision floating-point number as a string.
        /// </summary>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The context describing encoding of characters.</param>
        /// <param name="format">A standard or custom numeric format string.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        ValueTask WriteDoubleAsync(double value, StringLengthEncoding lengthFormat, EncodingContext context, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync(value, lengthFormat, context, format, provider, token);

        /// <summary>
        /// Encodes <see cref="Guid"/> as a string.
        /// </summary>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The context describing encoding of characters.</param>
        /// <param name="format">A standard or custom GUID format string.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        ValueTask WriteGuidAsync(Guid value, StringLengthEncoding lengthFormat, EncodingContext context, string? format = null, CancellationToken token = default)
            => WriteAsync(value.ToString(format, InvariantCulture).AsMemory(), context, lengthFormat, token);

        /// <summary>
        /// Encodes <see cref="DateTime"/> as a string.
        /// </summary>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The context describing encoding of characters.</param>
        /// <param name="format">A standard or custom date/time format string.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        ValueTask WriteDateTimeAsync(DateTime value, StringLengthEncoding lengthFormat, EncodingContext context, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync(value, lengthFormat, context, format, provider, token);

        /// <summary>
        /// Encodes <see cref="DateTimeOffset"/> as a string.
        /// </summary>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The context describing encoding of characters.</param>
        /// <param name="format">A standard or custom date/time format string.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        ValueTask WriteDateTimeOffsetAsync(DateTimeOffset value, StringLengthEncoding lengthFormat, EncodingContext context, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync(value, lengthFormat, context, format, provider, token);

        /// <summary>
        /// Encodes <see cref="TimeSpan"/> as a string.
        /// </summary>
        /// <param name="value">The value to encode.</param>
        /// <param name="lengthFormat">String length encoding format.</param>
        /// <param name="context">The context describing encoding of characters.</param>
        /// <param name="format">A standard or custom date/time format string.</param>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        ValueTask WriteTimeSpanAsync(TimeSpan value, StringLengthEncoding lengthFormat, EncodingContext context, string? format = null, IFormatProvider? provider = null, CancellationToken token = default)
            => WriteAsync(value, lengthFormat, context, format, provider, token);

        /// <summary>
        /// Encodes a block of memory.
        /// </summary>
        /// <param name="input">A block of memory.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        ValueTask WriteAsync(ReadOnlyMemory<byte> input, CancellationToken token = default);

        /// <summary>
        /// Encodes a block of characters using the specified encoding.
        /// </summary>
        /// <param name="chars">The characters to encode.</param>
        /// <param name="context">The context describing encoding of characters.</param>
        /// <param name="lengthFormat">String length encoding format; or <see langword="null"/> to prevent encoding of string length.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        ValueTask WriteAsync(ReadOnlyMemory<char> chars, EncodingContext context, StringLengthEncoding? lengthFormat, CancellationToken token = default);

        /// <summary>
        /// Writes the content from the specified stream.
        /// </summary>
        /// <param name="input">The stream to read from.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        async Task CopyFromAsync(Stream input, CancellationToken token = default)
        {
            const int defaultBufferSize = 512;
            using var buffer = new ByteBuffer(defaultBufferSize);
            for (int count; (count = await input.ReadAsync(buffer.Memory, token).ConfigureAwait(false)) > 0; )
                await WriteAsync(buffer.Memory.Slice(0, count), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Writes the content from the specified pipe.
        /// </summary>
        /// <param name="input">The pipe to read from.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        async Task CopyFromAsync(PipeReader input, CancellationToken token = default)
        {
            ReadResult result;
            do
            {
                result = await input.ReadAsync(token).ConfigureAwait(false);
                result.ThrowIfCancellationRequested();
                var buffer = result.Buffer;
                for (SequencePosition position = buffer.Start; buffer.TryGet(ref position, out var block); input.AdvanceTo(position))
                    await WriteAsync(block, token).ConfigureAwait(false);
            }
            while (!result.IsCompleted);
        }

        /// <summary>
        /// Writes the content from the specified sequence of bytes.
        /// </summary>
        /// <param name="input">The sequence of bytes to read from.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        async Task WriteAsync(ReadOnlySequence<byte> input, CancellationToken token = default)
        {
            foreach (var segment in input)
                await WriteAsync(segment, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Writes the content from the delegate supplying memory blocks.
        /// </summary>
        /// <remarks>
        /// Copy process will be stopped when <paramref name="supplier"/> returns empty <see cref="ReadOnlyMemory{T}"/>.
        /// </remarks>
        /// <param name="supplier">The delegate supplying memory blocks.</param>
        /// <param name="arg">The argument to be passed to the supplier.</param>
        /// <param name="token">The token that can be used to cancel operation.</param>
        /// <typeparam name="TArg">The type of the argument to be passed to the supplier.</typeparam>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        async Task CopyFromAsync<TArg>(Func<TArg, CancellationToken, ValueTask<ReadOnlyMemory<byte>>> supplier, TArg arg, CancellationToken token = default)
        {
            for (ReadOnlyMemory<byte> source; !(source = await supplier(arg, token).ConfigureAwait(false)).IsEmpty; )
                await WriteAsync(source, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates default implementation of binary writer for the stream.
        /// </summary>
        /// <remarks>
        /// It is recommended to use extension methods from <see cref="StreamExtensions"/> class
        /// for encoding data to the stream. This method is intended for situation
        /// when you need an object implementing <see cref="IAsyncBinaryWriter"/> interface.
        /// </remarks>
        /// <param name="output">The stream instance.</param>
        /// <param name="buffer">The buffer used for encoding binary data.</param>
        /// <returns>The stream writer.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException"><paramref name="buffer"/> is empty.</exception>
        public static IAsyncBinaryWriter Create(Stream output, Memory<byte> buffer)
            => new AsyncStreamBinaryWriter(output, buffer);

        /// <summary>
        /// Creates default implementation of binary writer for the pipe.
        /// </summary>
        /// <remarks>
        /// It is recommended to use extension methods from <see cref="Pipelines.PipeExtensions"/> class
        /// for encoding data to the pipe. This method is intended for situation
        /// when you need an object implementing <see cref="IAsyncBinaryWriter"/> interface.
        /// </remarks>
        /// <param name="output">The stream instance.</param>
        /// <returns>The binary writer.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
        public static IAsyncBinaryWriter Create(PipeWriter output)
            => new Pipelines.PipeBinaryWriter(output);

        /// <summary>
        /// Creates default implementation of binary writer for the pipe.
        /// </summary>
        /// <param name="output">The stream instance.</param>
        /// <param name="stringLengthThreshold">
        /// The threshold for the number of characters.
        /// If the number of characters is less than or equal to this threshold then
        /// writer encodes the whole sequence of characters in memory and then flushes the pipe;
        /// otherwise, the pipe flushes multiple times for each portion of the sequence.
        /// </param>
        /// <param name="encodingBufferSize">The size of internal buffer used to encode characters.</param>
        /// <returns>The binary writer.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="encodingBufferSize"/> or <paramref name="stringLengthThreshold"/> is less than zero.</exception>
        public static IAsyncBinaryWriter Create(PipeWriter output, int stringLengthThreshold, int encodingBufferSize)
        {
            if (stringLengthThreshold < 0)
                throw new ArgumentOutOfRangeException(nameof(stringLengthThreshold));
            if (encodingBufferSize < 0)
                throw new ArgumentOutOfRangeException(nameof(encodingBufferSize));

            return new Pipelines.PipeBinaryWriter(output, stringLengthThreshold, encodingBufferSize);
        }

        /// <summary>
        /// Creates default implementation of binary writer for the buffer writer.
        /// </summary>
        /// <typeparam name="TWriter">The type of the buffer writer.</typeparam>
        /// <param name="writer">The buffer writer.</param>
        /// <returns>The binary writer.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="writer"/> is <see langword="null"/>.</exception>
        public static IAsyncBinaryWriter Create<TWriter>(TWriter writer)
            where TWriter : class, IBufferWriter<byte>, IFlushable
            => new AsyncBufferWriter(writer);
    }
}