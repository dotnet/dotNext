using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace DotNext.IO;

using Buffers;
using Text;
using static Buffers.BufferReader;
using static Collections.Generic.Sequence;

/// <summary>
/// Represents high-level read/write methods for the stream.
/// </summary>
/// <remarks>
/// This class provides alternative way to read and write typed data from/to the stream
/// without instantiation of <see cref="BinaryReader"/> and <see cref="BinaryWriter"/>.
/// </remarks>
public static partial class StreamExtensions
{
    private const int BufferSizeForLength = 5;
    private const int DefaultBufferSize = 256;

    private static int Read7BitEncodedInt(this Stream stream)
    {
        var reader = new SevenBitEncodedInt.Reader();
        bool moveNext;
        do
        {
            var b = stream.ReadByte();
            moveNext = b >= 0 ? reader.Append((byte)b) : throw new EndOfStreamException();
        }
        while (moveNext);
        return (int)reader.Result;
    }

    private static async ValueTask<int> Read7BitEncodedIntAsync(this Stream stream, Memory<byte> buffer, CancellationToken token)
    {
        buffer = buffer.Slice(0, 1);
        var reader = new SevenBitEncodedInt.Reader();
        for (var moveNext = true; moveNext; moveNext = reader.Append(buffer.Span[0]))
        {
            var count = await stream.ReadAsync(buffer, token).ConfigureAwait(false);
            if (count == 0)
                throw new EndOfStreamException();
        }

        return (int)reader.Result;
    }

    /// <summary>
    /// Parses the value encoded as a set of characters.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="parser">The parser.</param>
    /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
    /// <param name="context">The decoding context containing string characters encoding.</param>
    /// <param name="buffer">The buffer that is allocated by the caller.</param>
    /// <param name="provider">The format provider.</param>
    /// <returns>The parsed value.</returns>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="FormatException">The string is in wrong format.</exception>
    [SkipLocalsInit]
    public static T Parse<T>(this Stream stream, Parser<T> parser, LengthFormat lengthFormat, in DecodingContext context, Span<byte> buffer, IFormatProvider? provider = null)
        where T : notnull
    {
        var length = ReadLength(stream, lengthFormat);
        if (length <= 0)
            throw new EndOfStreamException();

        using var result = length <= MemoryRental<char>.StackallocThreshold ? stackalloc char[length] : new MemoryRental<char>(length);
        length = ReadString(stream, result.Span, in context, buffer);
        return parser(result.Span.Slice(0, length), provider);
    }

    /// <summary>
    /// Parses the value encoded as a sequence of bytes.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="buffer">The buffer that is allocated by the caller.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding the value.</exception>
    [RequiresPreviewFeatures]
    public static T Parse<T>(this Stream stream, Span<byte> buffer)
        where T : notnull, IBinaryFormattable<T>
    {
        if (buffer.Length < T.Size)
            throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));

        buffer = buffer.Slice(0, T.Size);
        ReadBlock(stream, buffer);

        var reader = new SpanReader<byte>(buffer);
        return T.Parse(ref reader);
    }

    /// <summary>
    /// Parses the value encoded as a sequence of bytes.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="stream">The stream to read from.</param>
    /// <returns>The decoded value.</returns>
    [RequiresPreviewFeatures]
    public static T Parse<T>(this Stream stream)
        where T : notnull, IBinaryFormattable<T>
    {
        using var buffer = (uint)T.Size <= (uint)MemoryRental<byte>.StackallocThreshold
            ? stackalloc byte[T.Size]
            : new MemoryRental<byte>(T.Size);

        return Parse<T>(stream, buffer.Span);
    }

    private static int ReadString(Stream stream, Span<char> result, in DecodingContext context, Span<byte> buffer)
    {
        var maxChars = context.Encoding.GetMaxCharCount(buffer.Length);
        if (maxChars == 0)
            throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));
        var decoder = context.GetDecoder();
        var resultOffset = 0;
        for (int length = result.Length, n; length > 0; resultOffset += decoder.GetChars(buffer.Slice(0, n), result.Slice(resultOffset), length == 0))
        {
            n = stream.Read(buffer.TrimLength(length));
            if (n == 0)
                throw new EndOfStreamException();
            length -= n;
        }

        return resultOffset;
    }

    /// <summary>
    /// Reads the string using the specified encoding and supplied reusable buffer.
    /// </summary>
    /// <remarks>
    /// <paramref name="buffer"/> length can be less than <paramref name="length"/>
    /// but should be enough to decode at least one character of the specified encoding.
    /// </remarks>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="length">The length of the string, in bytes.</param>
    /// <param name="context">The text decoding context.</param>
    /// <param name="buffer">The buffer that is allocated by the caller.</param>
    /// <returns>The string decoded from the log entry content stream.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
    /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
    [SkipLocalsInit]
    public static string ReadString(this Stream stream, int length, in DecodingContext context, Span<byte> buffer)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        if (length == 0)
            return string.Empty;

        using var result = length <= MemoryRental<char>.StackallocThreshold ? stackalloc char[length] : new MemoryRental<char>(length);
        return new string(result.Span.Slice(0, ReadString(stream, result.Span, in context, buffer)));
    }

    /// <summary>
    /// Reads the string using the specified encoding and supplied reusable buffer.
    /// </summary>
    /// <remarks>
    /// <paramref name="buffer"/> length can be less than <paramref name="length"/>
    /// but should be enough to decode at least one character of the specified encoding.
    /// </remarks>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="length">The length of the string, in bytes.</param>
    /// <param name="context">The text decoding context.</param>
    /// <param name="buffer">The buffer that is allocated by the caller.</param>
    /// <param name="allocator">The allocator of the buffer of characters.</param>
    /// <returns>The buffer of characters.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
    /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
    public static MemoryOwner<char> ReadString(this Stream stream, int length, in DecodingContext context, Span<byte> buffer, MemoryAllocator<char>? allocator)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        MemoryOwner<char> result;
        if (length == 0)
        {
            result = default;
        }
        else
        {
            result = allocator.Invoke(length, exactSize: true);
            length = ReadString(stream, result.Span, in context, buffer);
            result.TryResize(length);
        }

        return result;
    }

    /// <summary>
    /// Decodes an arbitrary large big integer.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="length">The length of the value, in bytes.</param>
    /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
    /// <param name="buffer">The buffer that is allocated by the caller.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding the value.</exception>
    /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
    public static BigInteger ReadBigInteger(this Stream stream, int length, bool littleEndian, Span<byte> buffer)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        if (length == 0)
            return BigInteger.Zero;

        if (buffer.Length < length)
            throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));

        buffer = buffer.Slice(0, length);
        stream.ReadBlock(buffer);
        return new BigInteger(buffer, isBigEndian: !littleEndian);
    }

    /// <summary>
    /// Decodes an arbitrary large big integer.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="length">The length of the value, in bytes.</param>
    /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
    /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
    [SkipLocalsInit]
    public static BigInteger ReadBigInteger(this Stream stream, int length, bool littleEndian)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        if (length == 0)
            return BigInteger.Zero;

        using MemoryRental<byte> buffer = length <= MemoryRental<byte>.StackallocThreshold ? stackalloc byte[length] : new MemoryRental<byte>(length);
        stream.ReadBlock(buffer.Span);
        return new BigInteger(buffer.Span, isBigEndian: !littleEndian);
    }

    /// <summary>
    /// Decodes an arbitrary large big integer.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="lengthFormat">The format of the value length encoded in the underlying stream.</param>
    /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
    /// <param name="buffer">The buffer that is allocated by the caller.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding the value.</exception>
    /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static BigInteger ReadBigInteger(this Stream stream, LengthFormat lengthFormat, bool littleEndian, Span<byte> buffer)
        => ReadBigInteger(stream, stream.ReadLength(lengthFormat), littleEndian, buffer);

    /// <summary>
    /// Decodes an arbitrary large big integer.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="lengthFormat">The format of the value length encoded in the underlying stream.</param>
    /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static BigInteger ReadBigInteger(this Stream stream, LengthFormat lengthFormat, bool littleEndian)
        => ReadBigInteger(stream, stream.ReadLength(lengthFormat), littleEndian);

    private static int ReadLength(this Stream stream, LengthFormat lengthFormat)
    {
        int result;
        var littleEndian = BitConverter.IsLittleEndian;
        switch (lengthFormat)
        {
            default:
                throw new ArgumentOutOfRangeException(nameof(lengthFormat));
            case LengthFormat.Plain:
                result = stream.Read<int>();
                break;
            case LengthFormat.PlainLittleEndian:
                littleEndian = true;
                goto case LengthFormat.Plain;
            case LengthFormat.PlainBigEndian:
                littleEndian = false;
                goto case LengthFormat.Plain;
            case LengthFormat.Compressed:
                result = stream.Read7BitEncodedInt();
                break;
        }

        result.ReverseIfNeeded(littleEndian);
        return result;
    }

    private static async ValueTask<int> ReadLengthAsync(this Stream stream, LengthFormat lengthFormat, Memory<byte> buffer, CancellationToken token)
    {
        ValueTask<int> result;
        var littleEndian = BitConverter.IsLittleEndian;
        switch (lengthFormat)
        {
            default:
                throw new ArgumentOutOfRangeException(nameof(lengthFormat));
            case LengthFormat.Plain:
                result = stream.ReadAsync<int>(buffer, token);
                break;
            case LengthFormat.PlainLittleEndian:
                littleEndian = true;
                goto case LengthFormat.Plain;
            case LengthFormat.PlainBigEndian:
                littleEndian = false;
                goto case LengthFormat.Plain;
            case LengthFormat.Compressed:
                result = stream.Read7BitEncodedIntAsync(buffer, token);
                break;
        }

        var length = await result.ConfigureAwait(false);
        length.ReverseIfNeeded(littleEndian);
        return length;
    }

    /// <summary>
    /// Reads a length-prefixed string using the specified encoding and supplied reusable buffer.
    /// </summary>
    /// <remarks>
    /// This method decodes string length (in bytes) from
    /// stream in contrast to <see cref="ReadString(Stream, int, in DecodingContext, Span{byte})"/>.
    /// </remarks>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
    /// <param name="context">The text decoding context.</param>
    /// <param name="buffer">The buffer that is allocated by the caller.</param>
    /// <returns>The string decoded from the log entry content stream.</returns>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
    /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static string ReadString(this Stream stream, LengthFormat lengthFormat, in DecodingContext context, Span<byte> buffer)
        => ReadString(stream, stream.ReadLength(lengthFormat), in context, buffer);

    /// <summary>
    /// Reads a length-prefixed string using the specified encoding and supplied reusable buffer.
    /// </summary>
    /// <remarks>
    /// This method decodes string length (in bytes) from
    /// stream in contrast to <see cref="ReadString(Stream, int, in DecodingContext, Span{byte}, MemoryAllocator{char})"/>.
    /// </remarks>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
    /// <param name="context">The text decoding context.</param>
    /// <param name="buffer">The buffer that is allocated by the caller.</param>
    /// <param name="allocator">The allocator of the buffer of characters.</param>
    /// <returns>The buffer of characters.</returns>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
    /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static MemoryOwner<char> ReadString(this Stream stream, LengthFormat lengthFormat, in DecodingContext context, Span<byte> buffer, MemoryAllocator<char>? allocator)
        => ReadString(stream, stream.ReadLength(lengthFormat), in context, buffer, allocator);

    /// <summary>
    /// Reads the string using the specified encoding.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="length">The length of the string, in bytes.</param>
    /// <param name="encoding">The encoding used to decode bytes from stream into characters.</param>
    /// <returns>The string decoded from the log entry content stream.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
    /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
    [SkipLocalsInit]
    public static string ReadString(this Stream stream, int length, Encoding encoding)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        if (length == 0)
            return string.Empty;

        int charCount;
        using MemoryRental<char> charBuffer = length <= MemoryRental<char>.StackallocThreshold ? stackalloc char[length] : new MemoryRental<char>(length);

        using (MemoryRental<byte> bytesBuffer = length <= MemoryRental<byte>.StackallocThreshold ? stackalloc byte[length] : new MemoryRental<byte>(length))
        {
            stream.ReadBlock(bytesBuffer.Span);
            charCount = encoding.GetChars(bytesBuffer.Span, charBuffer.Span);
        }

        return new string(charBuffer.Span.Slice(0, charCount));
    }

    /// <summary>
    /// Reads the string using the specified encoding.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="length">The length of the string, in bytes.</param>
    /// <param name="encoding">The encoding used to decode bytes from stream into characters.</param>
    /// <param name="allocator">The allocator of the buffer of characters.</param>
    /// <returns>The buffer of characters.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
    /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
    public static MemoryOwner<char> ReadString(this Stream stream, int length, Encoding encoding, MemoryAllocator<char>? allocator)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        MemoryOwner<char> result;
        if (length == 0)
        {
            result = default;
        }
        else
        {
            using var bytesBuffer = length <= MemoryRental<byte>.StackallocThreshold ? stackalloc byte[length] : new MemoryRental<byte>(length);
            stream.ReadBlock(bytesBuffer.Span);
            result = encoding.GetChars(bytesBuffer.Span, allocator);
        }

        return result;
    }

    /// <summary>
    /// Reads a length-prefixed string using the specified encoding.
    /// </summary>
    /// <remarks>
    /// This method decodes string length (in bytes) from
    /// stream in contrast to <see cref="ReadString(Stream, int, Encoding)"/>.
    /// </remarks>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
    /// <param name="encoding">The encoding used to decode bytes from stream into characters.</param>
    /// <returns>The string decoded from the log entry content stream.</returns>
    /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static string ReadString(this Stream stream, LengthFormat lengthFormat, Encoding encoding)
        => ReadString(stream, stream.ReadLength(lengthFormat), encoding);

    /// <summary>
    /// Reads a length-prefixed string using the specified encoding.
    /// </summary>
    /// <remarks>
    /// This method decodes string length (in bytes) from
    /// stream in contrast to <see cref="ReadString(Stream, int, Encoding)"/>.
    /// </remarks>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
    /// <param name="encoding">The encoding used to decode bytes from stream into characters.</param>
    /// <param name="allocator">The allocator of the buffer of characters.</param>
    /// <returns>The buffer of characters.</returns>
    /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static MemoryOwner<char> ReadString(this Stream stream, LengthFormat lengthFormat, Encoding encoding, MemoryAllocator<char>? allocator)
        => ReadString(stream, stream.ReadLength(lengthFormat), encoding, allocator);

    /// <summary>
    /// Parses the value encoded as a set of characters.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="parser">The parser.</param>
    /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
    /// <param name="context">The decoding context containing string characters encoding.</param>
    /// <param name="buffer">The buffer that is allocated by the caller.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The parsed value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="FormatException">The string is in wrong format.</exception>
    public static async ValueTask<T> ParseAsync<T>(this Stream stream, Parser<T> parser, LengthFormat lengthFormat, DecodingContext context, Memory<byte> buffer, IFormatProvider? provider = null, CancellationToken token = default)
        where T : notnull
    {
        var length = await ReadLengthAsync(stream, lengthFormat, buffer, token).ConfigureAwait(false);
        if (length == 0)
            throw new EndOfStreamException();

        using var result = MemoryAllocator.Allocate<char>(length, true);
        length = await ReadStringAsync(stream, result.Memory, context, buffer, token).ConfigureAwait(false);
        return parser(result.Memory.Slice(0, length).Span, provider);
    }

    /// <summary>
    /// Parses the value encoded as a set of characters.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="parser">The parser.</param>
    /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
    /// <param name="context">The decoding context containing string characters encoding.</param>
    /// <param name="provider">The format provider.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The parsed value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="FormatException">The string is in wrong format.</exception>
    public static async ValueTask<T> ParseAsync<T>(this Stream stream, Parser<T> parser, LengthFormat lengthFormat, DecodingContext context, IFormatProvider? provider = null, CancellationToken token = default)
        where T : notnull
    {
        int length;
        MemoryOwner<byte> buffer;
        using (buffer = MemoryAllocator.Allocate<byte>(BufferSizeForLength, false))
            length = await ReadLengthAsync(stream, lengthFormat, buffer.Memory, token).ConfigureAwait(false);

        if (length == 0)
            throw new EndOfStreamException();

        using var result = MemoryAllocator.Allocate<char>(length, true);
        using (buffer = MemoryAllocator.Allocate<byte>(length, false))
        {
            length = await ReadStringAsync(stream, result.Memory, context, buffer.Memory, token).ConfigureAwait(false);
            return parser(result.Memory.Slice(0, length).Span, provider);
        }
    }

    /// <summary>
    /// Parses the value encoded as a sequence of bytes.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="buffer">The buffer that is allocated by the caller.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The parsed value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding value.</exception>
    [RequiresPreviewFeatures]
    public static async ValueTask<T> ParseAsync<T>(this Stream stream, Memory<byte> buffer, CancellationToken token = default)
        where T : notnull, IBinaryFormattable<T>
    {
        if (buffer.Length < T.Size)
            throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));

        buffer = buffer.Slice(0, T.Size);
        await stream.ReadAsync(buffer, token).ConfigureAwait(false);
        return IBinaryFormattable<T>.Parse(buffer.Span);
    }

    /// <summary>
    /// Parses the value encoded as a sequence of bytes.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The parsed value.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="EndOfStreamException">The underlying source doesn't contain necessary amount of bytes to decode the value.</exception>
    [RequiresPreviewFeatures]
    public static async ValueTask<T> ParseAsync<T>(this Stream stream, CancellationToken token = default)
        where T : notnull, IBinaryFormattable<T>
    {
        using var buffer = MemoryAllocator.Allocate<byte>(T.Size, true);
        return await ParseAsync<T>(stream, buffer.Memory, token).ConfigureAwait(false);
    }

    private static async ValueTask<int> ReadStringAsync(this Stream stream, Memory<char> result, DecodingContext context, Memory<byte> buffer, CancellationToken token)
    {
        var maxChars = context.Encoding.GetMaxCharCount(buffer.Length);
        if (maxChars == 0)
            throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));
        var decoder = context.GetDecoder();
        var resultOffset = 0;
        for (int length = result.Length, n; length > 0; resultOffset += decoder.GetChars(buffer.Span.Slice(0, n), result.Span.Slice(resultOffset), length == 0))
        {
            n = await stream.ReadAsync(buffer.TrimLength(length), token).ConfigureAwait(false);
            if (n == 0)
                throw new EndOfStreamException();
            length -= n;
        }

        return resultOffset;
    }

    /// <summary>
    /// Decodes an arbitrary integer value asynchronously.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="length">The length of the value, in bytes.</param>
    /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
    /// <param name="buffer">The buffer that is allocated by the caller.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding the value.</exception>
    /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask<BigInteger> ReadBigIntegerAsync(this Stream stream, int length, bool littleEndian, Memory<byte> buffer, CancellationToken token = default)
    {
        if (length == 0)
            return BigInteger.Zero;
        if (buffer.Length < length)
            throw new ArgumentException(ExceptionMessages.BufferTooSmall, nameof(buffer));

        buffer = buffer.Slice(0, length);
        await stream.ReadBlockAsync(buffer, token).ConfigureAwait(false);
        return new BigInteger(buffer.Span, isBigEndian: !littleEndian);
    }

    /// <summary>
    /// Decodes an arbitrary integer value asynchronously.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="length">The length of the value, in bytes.</param>
    /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask<BigInteger> ReadBigIntegerAsync(this Stream stream, int length, bool littleEndian, CancellationToken token = default)
    {
        if (length == 0)
            return BigInteger.Zero;

        using var buffer = MemoryAllocator.Allocate<byte>(length, true);
        await stream.ReadBlockAsync(buffer.Memory, token).ConfigureAwait(false);
        return new BigInteger(buffer.Span, isBigEndian: !littleEndian);
    }

    /// <summary>
    /// Decodes an arbitrary integer value asynchronously.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="lengthFormat">The format of the value length encoded in the underlying stream.</param>
    /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
    /// <param name="buffer">The buffer that is allocated by the caller.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding the value.</exception>
    /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static async ValueTask<BigInteger> ReadBigIntegerAsync(this Stream stream, LengthFormat lengthFormat, bool littleEndian, Memory<byte> buffer, CancellationToken token = default)
        => await ReadBigIntegerAsync(stream, await stream.ReadLengthAsync(lengthFormat, buffer, token).ConfigureAwait(false), littleEndian, buffer, token).ConfigureAwait(false);

    /// <summary>
    /// Decodes an arbitrary integer value asynchronously.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="lengthFormat">The format of the value length encoded in the underlying stream.</param>
    /// <param name="littleEndian"><see langword="true"/> if value is stored in the underlying binary stream as little-endian; otherwise, use big-endian.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The decoded value.</returns>
    /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static async ValueTask<BigInteger> ReadBigIntegerAsync(this Stream stream, LengthFormat lengthFormat, bool littleEndian, CancellationToken token = default)
    {
        using var lengthDecodingBuffer = MemoryAllocator.Allocate<byte>(BufferSizeForLength, false);
        return await ReadBigIntegerAsync(stream, await stream.ReadLengthAsync(lengthFormat, lengthDecodingBuffer.Memory, token).ConfigureAwait(false), littleEndian, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the string asynchronously using the specified encoding and supplied reusable buffer.
    /// </summary>
    /// <remarks>
    /// <paramref name="buffer"/> length can be less than <paramref name="length"/>
    /// but should be enough to decode at least one character of the specified encoding.
    /// </remarks>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="length">The length of the string, in bytes.</param>
    /// <param name="context">The text decoding context.</param>
    /// <param name="buffer">The buffer that is allocated by the caller.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The string decoded from the log entry content stream.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
    /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask<string> ReadStringAsync(this Stream stream, int length, DecodingContext context, Memory<byte> buffer, CancellationToken token = default)
    {
        using var chars = await ReadStringAsync(stream, length, context, buffer, null, token).ConfigureAwait(false);
        return chars.IsEmpty ? string.Empty : new string(chars.Span);
    }

    /// <summary>
    /// Reads the string asynchronously using the specified encoding and supplied reusable buffer.
    /// </summary>
    /// <remarks>
    /// <paramref name="buffer"/> length can be less than <paramref name="length"/>
    /// but should be enough to decode at least one character of the specified encoding.
    /// </remarks>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="length">The length of the string, in bytes.</param>
    /// <param name="context">The text decoding context.</param>
    /// <param name="buffer">The buffer that is allocated by the caller.</param>
    /// <param name="allocator">The allocator of the buffer of characters.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The buffer of characters.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
    /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask<MemoryOwner<char>> ReadStringAsync(this Stream stream, int length, DecodingContext context, Memory<byte> buffer, MemoryAllocator<char>? allocator, CancellationToken token = default)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        MemoryOwner<char> result;
        if (length == 0)
        {
            result = default;
        }
        else
        {
            result = allocator.Invoke(length, exactSize: true);
            length = await ReadStringAsync(stream, result.Memory, context, buffer, token).ConfigureAwait(false);
            result.TryResize(length);
        }

        return result;
    }

    /// <summary>
    /// Reads a length-prefixed string asynchronously using the specified encoding and supplied reusable buffer.
    /// </summary>
    /// <remarks>
    /// This method decodes string length (in bytes) from
    /// stream in contrast to <see cref="ReadStringAsync(Stream, int, DecodingContext, Memory{byte}, CancellationToken)"/>.
    /// </remarks>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
    /// <param name="context">The text decoding context.</param>
    /// <param name="buffer">The buffer that is allocated by the caller.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The string decoded from the log entry content stream.</returns>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
    /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static async ValueTask<string> ReadStringAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, Memory<byte> buffer, CancellationToken token = default)
        => await ReadStringAsync(stream, await stream.ReadLengthAsync(lengthFormat, buffer, token).ConfigureAwait(false), context, buffer, token).ConfigureAwait(false);

    /// <summary>
    /// Reads a length-prefixed string asynchronously using the specified encoding and supplied reusable buffer.
    /// </summary>
    /// <remarks>
    /// This method decodes string length (in bytes) from
    /// stream in contrast to <see cref="ReadStringAsync(Stream, int, DecodingContext, Memory{byte}, MemoryAllocator{char}, CancellationToken)"/>.
    /// </remarks>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
    /// <param name="context">The text decoding context.</param>
    /// <param name="buffer">The buffer that is allocated by the caller.</param>
    /// <param name="allocator">The allocator of the buffer of characters.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The buffer of characters.</returns>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> too small for decoding characters.</exception>
    /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static async ValueTask<MemoryOwner<char>> ReadStringAsync(this Stream stream, LengthFormat lengthFormat, DecodingContext context, Memory<byte> buffer, MemoryAllocator<char>? allocator, CancellationToken token = default)
        => await ReadStringAsync(stream, await stream.ReadLengthAsync(lengthFormat, buffer, token).ConfigureAwait(false), context, buffer, allocator, token).ConfigureAwait(false);

    /// <summary>
    /// Reads the string asynchronously using the specified encoding and supplied reusable buffer.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="length">The length of the string, in bytes.</param>
    /// <param name="encoding">The encoding used to decode bytes from stream into characters.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The string decoded from the log entry content stream.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
    /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask<string> ReadStringAsync(this Stream stream, int length, Encoding encoding, CancellationToken token = default)
    {
        using var chars = await ReadStringAsync(stream, length, encoding, null, token).ConfigureAwait(false);
        return chars.IsEmpty ? string.Empty : new string(chars.Span);
    }

    /// <summary>
    /// Reads the string asynchronously using the specified encoding and supplied reusable buffer.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="length">The length of the string, in bytes.</param>
    /// <param name="encoding">The encoding used to decode bytes from stream into characters.</param>
    /// <param name="allocator">The allocator of the buffer of characters.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The buffer of characters.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is less than zero.</exception>
    /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask<MemoryOwner<char>> ReadStringAsync(this Stream stream, int length, Encoding encoding, MemoryAllocator<char>? allocator, CancellationToken token = default)
    {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        MemoryOwner<char> result;
        if (length == 0)
        {
            result = default;
        }
        else
        {
            using var bytesBuffer = MemoryAllocator.Allocate<byte>(length, exactSize: true);
            await stream.ReadBlockAsync(bytesBuffer.Memory, token).ConfigureAwait(false);
            result = encoding.GetChars(bytesBuffer.Span, allocator);
        }

        return result;
    }

    /// <summary>
    /// Reads a length-prefixed string asynchronously using the specified encoding and supplied reusable buffer.
    /// </summary>
    /// <remarks>
    /// This method decodes string length (in bytes) from
    /// stream in contrast to <see cref="ReadStringAsync(Stream, int, Encoding, CancellationToken)"/>.
    /// </remarks>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
    /// <param name="encoding">The encoding used to decode bytes from stream into characters.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The string decoded from the log entry content stream.</returns>
    /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static async ValueTask<string> ReadStringAsync(this Stream stream, LengthFormat lengthFormat, Encoding encoding, CancellationToken token = default)
    {
        using var lengthDecodingBuffer = MemoryAllocator.Allocate<byte>(BufferSizeForLength, exactSize: false);
        return await ReadStringAsync(stream, await stream.ReadLengthAsync(lengthFormat, lengthDecodingBuffer.Memory, token).ConfigureAwait(false), encoding, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads a length-prefixed string asynchronously using the specified encoding and supplied reusable buffer.
    /// </summary>
    /// <remarks>
    /// This method decodes string length (in bytes) from
    /// stream in contrast to <see cref="ReadStringAsync(Stream, int, Encoding, MemoryAllocator{char}, CancellationToken)"/>.
    /// </remarks>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="lengthFormat">The format of the string length encoded in the stream.</param>
    /// <param name="encoding">The encoding used to decode bytes from stream into characters.</param>
    /// <param name="allocator">The allocator of the buffer of characters.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The buffer of characters.</returns>
    /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
    public static async ValueTask<MemoryOwner<char>> ReadStringAsync(this Stream stream, LengthFormat lengthFormat, Encoding encoding, MemoryAllocator<char>? allocator, CancellationToken token = default)
    {
        using var lengthDecodingBuffer = MemoryAllocator.Allocate<byte>(BufferSizeForLength, exactSize: false);
        return await ReadStringAsync(stream, await stream.ReadLengthAsync(lengthFormat, lengthDecodingBuffer.Memory, token).ConfigureAwait(false), encoding, allocator, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads exact number of bytes.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="output">A region of memory. When this method returns, the contents of this region are replaced by the bytes read from the current source.</param>
    /// <exception cref="EndOfStreamException">The end of the stream is reached.</exception>
    public static void ReadBlock(this Stream stream, Span<byte> output)
    {
        for (int size = output.Length, bytesRead, offset = 0; size > 0; size -= bytesRead, offset += bytesRead)
        {
            bytesRead = stream.Read(output.Slice(offset, size));
            if (bytesRead == 0)
                throw new EndOfStreamException();
        }
    }

    /// <summary>
    /// Decodes the block of bytes.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="lengthFormat">The format of the block length encoded in the underlying stream.</param>
    /// <param name="allocator">The memory allocator used to place the decoded block of bytes.</param>
    /// <returns>The decoded block of bytes.</returns>
    /// <exception cref="EndOfStreamException">The end of the stream is reached.</exception>
    public static MemoryOwner<byte> ReadBlock(this Stream stream, LengthFormat lengthFormat, MemoryAllocator<byte>? allocator = null)
    {
        var length = stream.ReadLength(lengthFormat);
        MemoryOwner<byte> result;
        if (length > 0)
        {
            result = allocator.Invoke(length, true);
            stream.ReadBlock(result.Span);
        }
        else
        {
            result = default;
        }

        return result;
    }

    /// <summary>
    /// Deserializes the value type from the stream.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <typeparam name="T">The value type to be deserialized.</typeparam>
    /// <returns>The value deserialized from the stream.</returns>
    /// <exception cref="EndOfStreamException">The end of the stream is reached.</exception>
    public static unsafe T Read<T>(this Stream stream)
        where T : unmanaged
    {
        var result = default(T);
        stream.ReadBlock(Span.AsBytes(ref result));
        return result;
    }

    /// <summary>
    /// Reads exact number of bytes asynchronously.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="output">A region of memory. When this method returns, the contents of this region are replaced by the bytes read from the current source.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="EndOfStreamException">The end of the stream is reached.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask ReadBlockAsync(this Stream stream, Memory<byte> output, CancellationToken token = default)
    {
        for (int size = output.Length, bytesRead, offset = 0; size > 0; size -= bytesRead, offset += bytesRead)
        {
            bytesRead = await stream.ReadAsync(output.Slice(offset, size), token).ConfigureAwait(false);
            if (bytesRead == 0)
                throw new EndOfStreamException();
        }
    }

    /// <summary>
    /// Decodes the block of bytes asynchronously.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="lengthFormat">The format of the block length encoded in the underlying stream.</param>
    /// <param name="buffer">The buffer that is allocated by the caller.</param>
    /// <param name="allocator">The memory allocator used to place the decoded block of bytes.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The decoded block of bytes.</returns>
    /// <exception cref="EndOfStreamException">The end of the stream is reached.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask<MemoryOwner<byte>> ReadBlockAsync(this Stream stream, LengthFormat lengthFormat, Memory<byte> buffer, MemoryAllocator<byte>? allocator = null, CancellationToken token = default)
    {
        var length = await stream.ReadLengthAsync(lengthFormat, buffer, token).ConfigureAwait(false);
        MemoryOwner<byte> result;
        if (length > 0)
        {
            result = allocator.Invoke(length, true);
            await stream.ReadBlockAsync(result.Memory, token).ConfigureAwait(false);
        }
        else
        {
            result = default;
        }

        return result;
    }

    /// <summary>
    /// Asynchronously deserializes the value type from the stream.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="buffer">The buffer that is allocated by the caller.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <typeparam name="T">The value type to be deserialized.</typeparam>
    /// <returns>The value deserialized from the stream.</returns>
    /// <exception cref="EndOfStreamException">The end of the stream is reached.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask<T> ReadAsync<T>(this Stream stream, Memory<byte> buffer, CancellationToken token = default)
        where T : unmanaged
    {
        await stream.ReadBlockAsync(buffer.Slice(0, Unsafe.SizeOf<T>()), token).ConfigureAwait(false);
        return MemoryMarshal.Read<T>(buffer.Span);
    }

    internal static async ValueTask<TOutput> ReadAsync<TInput, TOutput, TConverter>(this Stream stream, TConverter converter, Memory<byte> buffer, CancellationToken token = default)
        where TInput : unmanaged
        where TOutput : unmanaged
        where TConverter : struct, ISupplier<TInput, TOutput>
    {
        await stream.ReadBlockAsync(buffer.Slice(0, Unsafe.SizeOf<TInput>()), token).ConfigureAwait(false);
        return converter.Invoke(MemoryMarshal.Read<TInput>(buffer.Span));
    }

    /// <summary>
    /// Asynchronously deserializes the value type from the stream.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <typeparam name="T">The value type to be deserialized.</typeparam>
    /// <returns>The value deserialized from the stream.</returns>
    /// <exception cref="EndOfStreamException">The end of the stream is reached.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask<T> ReadAsync<T>(this Stream stream, CancellationToken token = default)
        where T : unmanaged
    {
        using var buffer = MemoryAllocator.Allocate<byte>(Unsafe.SizeOf<T>(), true);
        await stream.ReadBlockAsync(buffer.Memory, token).ConfigureAwait(false);
        return MemoryMarshal.Read<T>(buffer.Span);
    }

    /// <summary>
    /// Asynchronously reads the bytes from the source stream and passes them to the consumer, using a specified buffer.
    /// </summary>
    /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
    /// <param name="source">The source stream to read from.</param>
    /// <param name="consumer">The destination stream to write into.</param>
    /// <param name="buffer">The buffer used to hold copied content temporarily.</param>
    /// <param name="token">The token that can be used to cancel this operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async Task CopyToAsync<TConsumer>(this Stream source, TConsumer consumer, Memory<byte> buffer, CancellationToken token = default)
        where TConsumer : notnull, ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>
    {
        for (int count; (count = await source.ReadAsync(buffer, token).ConfigureAwait(false)) > 0;)
        {
            await consumer.Invoke(buffer.Slice(0, count), token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Asynchronously reads the bytes from the source stream and writes them to another stream, using a specified buffer.
    /// </summary>
    /// <param name="source">The source stream to read from.</param>
    /// <param name="destination">The destination stream to write into.</param>
    /// <param name="buffer">The buffer used to hold copied content temporarily.</param>
    /// <param name="token">The token that can be used to cancel this operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static Task CopyToAsync(this Stream source, Stream destination, Memory<byte> buffer, CancellationToken token = default)
        => CopyToAsync<StreamConsumer>(source, destination, buffer, token);

    /// <summary>
    /// Synchronously reads the bytes from the source stream and passes them to the consumer, using a specified buffer.
    /// </summary>
    /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
    /// <param name="source">The source stream to read from.</param>
    /// <param name="consumer">The destination stream to write into.</param>
    /// <param name="buffer">The buffer used to hold copied content temporarily.</param>
    /// <param name="token">The token that can be used to cancel this operation.</param>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static void CopyTo<TConsumer>(this Stream source, TConsumer consumer, Span<byte> buffer, CancellationToken token = default)
        where TConsumer : notnull, IReadOnlySpanConsumer<byte>
    {
        for (int count; (count = source.Read(buffer)) > 0; token.ThrowIfCancellationRequested())
        {
            consumer.Invoke(buffer.Slice(0, count));
        }
    }

    /// <summary>
    /// Synchronously reads the bytes from the source stream and writes them to another stream, using a specified buffer.
    /// </summary>
    /// <param name="source">The source stream to read from.</param>
    /// <param name="destination">The destination stream to write into.</param>
    /// <param name="buffer">The buffer used to hold copied content temporarily.</param>
    /// <param name="token">The token that can be used to cancel this operation.</param>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static void CopyTo(this Stream source, Stream destination, Span<byte> buffer, CancellationToken token = default)
        => CopyTo<StreamConsumer>(source, destination, buffer, token);

    /// <summary>
    /// Reads the entire content using the specified delegate.
    /// </summary>
    /// <typeparam name="TArg">The type of the argument to be passed to the content reader.</typeparam>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="reader">The content reader.</param>
    /// <param name="arg">The argument to be passed to the content reader.</param>
    /// <param name="buffer">The buffer allocated by the caller.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> is empty.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static void CopyTo<TArg>(this Stream stream, ReadOnlySpanAction<byte, TArg> reader, TArg arg, Span<byte> buffer, CancellationToken token = default)
        => CopyTo(stream, new DelegatingReadOnlySpanConsumer<byte, TArg>(reader, arg), buffer, token);

    /// <summary>
    /// Reads the entire content using the specified delegate.
    /// </summary>
    /// <typeparam name="TArg">The type of the argument to be passed to the content reader.</typeparam>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="reader">The content reader.</param>
    /// <param name="arg">The argument to be passed to the content reader.</param>
    /// <param name="bufferSize">The size of the buffer used to read data.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bufferSize"/> is less than or equal to zero.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    [SkipLocalsInit]
    public static void CopyTo<TArg>(this Stream stream, ReadOnlySpanAction<byte, TArg> reader, TArg arg, int bufferSize = DefaultBufferSize, CancellationToken token = default)
    {
        if (bufferSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(bufferSize));

        using var owner = bufferSize <= MemoryRental<byte>.StackallocThreshold ? new MemoryRental<byte>(stackalloc byte[bufferSize]) : new MemoryRental<byte>(bufferSize);
        CopyTo(stream, reader, arg, owner.Span, token);
    }

    /// <summary>
    /// Reads the entire content using the specified delegate.
    /// </summary>
    /// <typeparam name="TArg">The type of the argument to be passed to the content reader.</typeparam>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="reader">The content reader.</param>
    /// <param name="arg">The argument to be passed to the content reader.</param>
    /// <param name="buffer">The buffer allocated by the caller.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> is empty.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static Task CopyToAsync<TArg>(this Stream stream, ReadOnlySpanAction<byte, TArg> reader, TArg arg, Memory<byte> buffer, CancellationToken token = default)
        => CopyToAsync(stream, new DelegatingReadOnlySpanConsumer<byte, TArg>(reader, arg), buffer, token);

    /// <summary>
    /// Reads the entire content using the specified delegate.
    /// </summary>
    /// <typeparam name="TArg">The type of the argument to be passed to the content reader.</typeparam>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="reader">The content reader.</param>
    /// <param name="arg">The argument to be passed to the content reader.</param>
    /// <param name="bufferSize">The size of the buffer used to read data.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bufferSize"/> is less than or equal to zero.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async Task CopyToAsync<TArg>(this Stream stream, ReadOnlySpanAction<byte, TArg> reader, TArg arg, int bufferSize = DefaultBufferSize, CancellationToken token = default)
    {
        if (bufferSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(bufferSize));

        using var owner = MemoryAllocator.Allocate<byte>(bufferSize, false);
        await CopyToAsync(stream, reader, arg, owner.Memory, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the entire content using the specified delegate.
    /// </summary>
    /// <typeparam name="TArg">The type of the argument to be passed to the content reader.</typeparam>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="reader">The content reader.</param>
    /// <param name="arg">The argument to be passed to the content reader.</param>
    /// <param name="buffer">The buffer allocated by the caller.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="ArgumentException"><paramref name="buffer"/> is empty.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static Task CopyToAsync<TArg>(this Stream stream, Func<TArg, ReadOnlyMemory<byte>, CancellationToken, ValueTask> reader, TArg arg, Memory<byte> buffer, CancellationToken token = default)
        => CopyToAsync(stream, new DelegatingMemoryConsumer<byte, TArg>(reader, arg), buffer, token);

    /// <summary>
    /// Reads the entire content using the specified delegate.
    /// </summary>
    /// <typeparam name="TArg">The type of the argument to be passed to the content reader.</typeparam>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="reader">The content reader.</param>
    /// <param name="arg">The argument to be passed to the content reader.</param>
    /// <param name="bufferSize">The size of the buffer used to read data.</param>
    /// <param name="token">The token that can be used to cancel operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bufferSize"/> is less than or equal to zero.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async Task CopyToAsync<TArg>(this Stream stream, Func<TArg, ReadOnlyMemory<byte>, CancellationToken, ValueTask> reader, TArg arg, int bufferSize = DefaultBufferSize, CancellationToken token = default)
    {
        if (bufferSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(bufferSize));

        using var owner = MemoryAllocator.Allocate<byte>(bufferSize, false);
        await CopyToAsync(stream, reader, arg, owner.Memory, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously reads the bytes from the current stream and writes them to buffer
    /// writer, using a specified cancellation token.
    /// </summary>
    /// <param name="source">The source stream.</param>
    /// <param name="destination">The writer to which the contents of the current stream will be copied.</param>
    /// <param name="bufferSize">The size, in bytes, of the buffer.</param>
    /// <param name="token">The token to monitor for cancellation requests.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bufferSize"/> is negative or zero.</exception>
    /// <exception cref="NotSupportedException"><paramref name="source"/> doesn't support reading.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async Task CopyToAsync(this Stream source, IBufferWriter<byte> destination, int bufferSize = 0, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (bufferSize < 0)
            throw new ArgumentOutOfRangeException(nameof(bufferSize));

        for (int count; ; destination.Advance(count))
        {
            var buffer = destination.GetMemory(bufferSize);
            count = await source.ReadAsync(buffer, token).ConfigureAwait(false);
            if (count <= 0)
                break;
        }
    }

    /// <summary>
    /// Synchronously reads the bytes from the current stream and writes them to buffer
    /// writer, using a specified cancellation token.
    /// </summary>
    /// <param name="source">The source stream.</param>
    /// <param name="destination">The writer to which the contents of the current stream will be copied.</param>
    /// <param name="bufferSize">The size, in bytes, of the buffer.</param>
    /// <param name="token">The token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bufferSize"/> is negative or zero.</exception>
    /// <exception cref="NotSupportedException"><paramref name="source"/> doesn't support reading.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static void CopyTo(this Stream source, IBufferWriter<byte> destination, int bufferSize = 0, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (bufferSize < 0)
            throw new ArgumentOutOfRangeException(nameof(bufferSize));

        for (int count; ; token.ThrowIfCancellationRequested())
        {
            var buffer = destination.GetSpan(bufferSize);
            count = source.Read(buffer);
            if (count <= 0)
                break;
            destination.Advance(count);
        }
    }

    /// <summary>
    /// Combines multiple readable streams.
    /// </summary>
    /// <param name="stream">The stream to combine.</param>
    /// <param name="others">A collection of streams.</param>
    /// <returns>An object that represents multiple streams as one logical stream.</returns>
    public static Stream Combine(this Stream stream, params Stream[] others)
        => others.IsNullOrEmpty() ? stream : new SparseStream(Singleton(stream).Concat(others));

    /// <summary>
    /// Combines multiple readable streams.
    /// </summary>
    /// <param name="streams">A collection of readable streams.</param>
    /// <returns>An object that represents multiple streams as one logical stream.</returns>
    public static Stream Combine(this IEnumerable<Stream> streams) => new SparseStream(streams);

    /// <summary>
    /// Reads at least the specified number of bytes.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="minimumSize">The minimum size to read.</param>
    /// <param name="buffer">A region of memory. When this method returns, the contents of this region are replaced by the bytes read from the current source.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The actual number of bytes written to the <paramref name="buffer"/>. This value is equal to or greater than <paramref name="minimumSize"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minimumSize"/> is greater than the length of <paramref name="buffer"/>.</exception>
    /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static async ValueTask<int> ReadAtLeastAsync(this Stream stream, int minimumSize, Memory<byte> buffer, CancellationToken token = default)
    {
        if ((uint)minimumSize > (uint)buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(minimumSize));

        var offset = 0;

        for (int size = minimumSize, bytesRead; size > 0; size -= bytesRead, offset += bytesRead)
        {
            bytesRead = await stream.ReadAsync(buffer.Slice(offset), token).ConfigureAwait(false);
            if (bytesRead is 0)
                throw new EndOfStreamException();
        }

        return offset;
    }

    /// <summary>
    /// Reads at least the specified number of bytes.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="minimumSize">The minimum size to read.</param>
    /// <param name="buffer">A region of memory. When this method returns, the contents of this region are replaced by the bytes read from the current source.</param>
    /// <returns>The actual number of bytes written to the <paramref name="buffer"/>. This value is equal to or greater than <paramref name="minimumSize"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minimumSize"/> is greater than the length of <paramref name="buffer"/>.</exception>
    /// <exception cref="EndOfStreamException">Unexpected end of stream.</exception>
    public static int ReadAtLeast(this Stream stream, int minimumSize, Span<byte> buffer)
    {
        if ((uint)minimumSize > (uint)buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(minimumSize));

        var offset = 0;

        for (int size = minimumSize, bytesRead; size > 0; size -= bytesRead, offset += bytesRead)
        {
            bytesRead = stream.Read(buffer.Slice(offset));
            if (bytesRead is 0)
                throw new EndOfStreamException();
        }

        return offset;
    }
}
