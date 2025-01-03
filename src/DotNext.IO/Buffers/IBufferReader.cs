using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace DotNext.Buffers;

using Binary;
using Numerics;
using DecodingContext = DotNext.Text.DecodingContext;

/// <summary>
/// Represents buffer reader.
/// </summary>
public interface IBufferReader : IReadOnlySpanConsumer<byte>
{
    /// <summary>
    /// The expected number of bytes to be consumed this reader.
    /// </summary>
    int RemainingBytes { get; }

    /// <summary>
    /// Gets a value indicating that reader doesn't support decoding of partial data.
    /// </summary>
    static virtual bool ThrowOnPartialData => true;
}

[StructLayout(LayoutKind.Auto)]
internal struct MemoryBlockReader(Memory<byte> destination) : IBufferReader
{
    readonly int IBufferReader.RemainingBytes => destination.Length;

    void IReadOnlySpanConsumer<byte>.Invoke(ReadOnlySpan<byte> source)
    {
        source.CopyTo(destination.Span, out var count);
        destination = destination.Slice(count);
    }
}

[StructLayout(LayoutKind.Auto)]
internal struct MemoryReader(Memory<byte> destination) : IBufferReader, ISupplier<int>
{
    private int bytesWritten;

    internal readonly int BytesWritten => bytesWritten;

    readonly int IBufferReader.RemainingBytes => destination.Length;

    void IReadOnlySpanConsumer<byte>.Invoke(ReadOnlySpan<byte> source)
    {
        source.CopyTo(destination.Span, out var count);
        bytesWritten += count;
        destination = default;
    }

    readonly int ISupplier<int>.Invoke() => BytesWritten;

    static bool IBufferReader.ThrowOnPartialData => false;
}

[StructLayout(LayoutKind.Auto)]
internal unsafe struct WellKnownIntegerReader<T>(delegate*<ReadOnlySpan<byte>, bool, T> parser) : IBufferReader, ISupplier<T>
    where T : IBinaryInteger<T>
{
    private T? buffer;
    private int writtenBytes;

    private Span<byte> Buffer => MemoryMarshal.CreateSpan(ref Unsafe.As<T?, byte>(ref buffer), Unsafe.SizeOf<T>());

    readonly int IBufferReader.RemainingBytes => Unsafe.SizeOf<T>() - writtenBytes;

    void IReadOnlySpanConsumer<byte>.Invoke(ReadOnlySpan<byte> source)
    {
        source.CopyTo(Buffer.Slice(writtenBytes), out var count);
        writtenBytes += count;
    }

    T ISupplier<T>.Invoke() => parser(Buffer, Number.IsSigned<T>() is false);

    internal static WellKnownIntegerReader<T> LittleEndian() => new(&T.ReadLittleEndian);

    internal static WellKnownIntegerReader<T> BigEndian() => new(&T.ReadBigEndian);
}

[StructLayout(LayoutKind.Auto)]
internal unsafe struct IntegerReader<T>(delegate*<ReadOnlySpan<byte>, bool, T> parser) : IBufferReader, ISupplier<T>
    where T : IBinaryInteger<T>
{
    private MemoryOwner<byte> buffer = Memory.AllocateExactly<byte>(Number.GetMaxByteCount<T>());
    private int writtenBytes;

    readonly int IBufferReader.RemainingBytes => buffer.Length - writtenBytes;

    void IReadOnlySpanConsumer<byte>.Invoke(ReadOnlySpan<byte> source)
    {
        source.CopyTo(buffer.Span.Slice(writtenBytes), out var count);
        writtenBytes += count;
    }

    T ISupplier<T>.Invoke()
    {
        try
        {
            return parser(buffer.Span.Slice(0, writtenBytes), Number.IsSigned<T>() is false);
        }
        finally
        {
            buffer.Dispose();
        }
    }

    internal static IntegerReader<T> LittleEndian() => new(&T.ReadLittleEndian);

    internal static IntegerReader<T> BigEndian() => new(&T.ReadBigEndian);
}

[StructLayout(LayoutKind.Auto)]
internal struct BinaryFormattable256Reader<T> : IBufferReader, ISupplier<T>
    where T : IBinaryFormattable<T>
{
    private Buffer256 buffer;
    private int writtenBytes;

    readonly int IBufferReader.RemainingBytes => T.Size - writtenBytes;

    void IReadOnlySpanConsumer<byte>.Invoke(ReadOnlySpan<byte> source)
    {
        Span<byte> destination = buffer;
        source.CopyTo(destination.Slice(writtenBytes), out var count);
        writtenBytes += count;
    }

    readonly T ISupplier<T>.Invoke()
    {
        ReadOnlySpan<byte> source = buffer;
        return T.Parse(source.Slice(0, writtenBytes));
    }

    internal static int MaxSize => Unsafe.SizeOf<Buffer256>();
}

[StructLayout(LayoutKind.Auto)]
internal struct BinaryFormattableReader<T>() : IBufferReader, ISupplier<T>
    where T : IBinaryFormattable<T>
{
    private MemoryOwner<byte> buffer = Memory.AllocateExactly<byte>(T.Size);
    private int writtenBytes;

    readonly int IBufferReader.RemainingBytes => T.Size - writtenBytes;

    void IReadOnlySpanConsumer<byte>.Invoke(ReadOnlySpan<byte> source)
    {
        source.CopyTo(buffer.Span.Slice(writtenBytes), out var count);
        writtenBytes += count;
    }

    T ISupplier<T>.Invoke()
    {
        try
        {
            return T.Parse(buffer.Span.Slice(0, writtenBytes));
        }
        finally
        {
            buffer.Dispose();
        }
    }
}

[StructLayout(LayoutKind.Auto)]
internal struct CharBufferDecodingReader(in DecodingContext context, int length, Memory<char> buffer) : IBufferReader, ISupplier<int>
{
    private readonly Decoder decoder = context.GetDecoder();
    private int remainingBytes = length, writtenChars;

    readonly int ISupplier<int>.Invoke() => writtenChars;

    readonly int IBufferReader.RemainingBytes => remainingBytes;

    void IReadOnlySpanConsumer<byte>.Invoke(ReadOnlySpan<byte> bytes)
    {
        remainingBytes -= bytes.Length;
        writtenChars += decoder.GetChars(bytes, buffer.Span.Slice(writtenChars), remainingBytes is 0);
    }
}

[StructLayout(LayoutKind.Auto)]
internal struct DecodingReader(Decoder decoder, int length, Memory<char> buffer) : IBufferReader, ISupplier<int>
{
    private int writtenChars;

    public readonly int RemainingBytes => Math.Min(length, buffer.Length);

    void IReadOnlySpanConsumer<byte>.Invoke(ReadOnlySpan<byte> source)
    {
        writtenChars = decoder.GetChars(source, buffer.Span, length <= source.Length);
        length = 0;
    }

    readonly int ISupplier<int>.Invoke() => writtenChars;
}

[StructLayout(LayoutKind.Auto)]
internal unsafe struct Parsing256Reader<TArg, TResult>(TArg arg, delegate*<ReadOnlySpan<byte>, TArg, TResult> parser, int length) : IBufferReader, ISupplier<TResult>
{
    private Buffer256 buffer;
    private int consumedBytes;

    readonly int IBufferReader.RemainingBytes => length - consumedBytes;

    void IReadOnlySpanConsumer<byte>.Invoke(ReadOnlySpan<byte> source)
    {
        Span<byte> destination = buffer;
        source.CopyTo(destination.Slice(consumedBytes), out var count);
        consumedBytes += count;
    }

    readonly TResult ISupplier<TResult>.Invoke()
    {
        ReadOnlySpan<byte> source = buffer;
        return parser(source.Slice(0, consumedBytes), arg);
    }

    internal static int MaxSize => Unsafe.SizeOf<Buffer256>();
}

[StructLayout(LayoutKind.Auto)]
internal unsafe struct ParsingReader<TArg, TResult>(TArg arg, delegate*<ReadOnlySpan<byte>, TArg, TResult> parser, int length) : IBufferReader, ISupplier<TResult>
{
    private MemoryOwner<byte> buffer = Memory.AllocateExactly<byte>(length);
    private int consumedBytes;

    readonly int IBufferReader.RemainingBytes => buffer.Length - consumedBytes;

    void IReadOnlySpanConsumer<byte>.Invoke(ReadOnlySpan<byte> source)
    {
        source.CopyTo(buffer.Span.Slice(consumedBytes), out var count);
        consumedBytes += count;
    }

    TResult ISupplier<TResult>.Invoke()
    {
        try
        {
            return parser(buffer.Span.Slice(0, consumedBytes), arg);
        }
        finally
        {
            buffer.Dispose();
        }
    }
}

[StructLayout(LayoutKind.Auto)]
internal struct SkippingReader(long length) : IBufferReader
{
    readonly int IBufferReader.RemainingBytes => int.CreateSaturating(length);

    void IReadOnlySpanConsumer<byte>.Invoke(ReadOnlySpan<byte> source)
        => length -= source.Length;
}

internal struct ProxyReader<TReader>(TReader reader) : IBufferReader, ISupplier<TReader>
    where TReader : struct, IBufferReader
{
    int IBufferReader.RemainingBytes => reader.RemainingBytes;

    void IReadOnlySpanConsumer<byte>.Invoke(ReadOnlySpan<byte> source)
        => reader.Invoke(source);

    static bool IBufferReader.ThrowOnPartialData => TReader.ThrowOnPartialData;

    readonly TReader ISupplier<TReader>.Invoke() => reader;

    public static implicit operator ProxyReader<TReader>(TReader reader) => new(reader);
}