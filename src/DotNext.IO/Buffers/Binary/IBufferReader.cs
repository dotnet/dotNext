using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace DotNext.Buffers.Binary;

using Numerics;
using DecodingContext = DotNext.Text.DecodingContext;

/// <summary>
/// Represents buffer reader.
/// </summary>
public interface IBufferReader
{
    /// <summary>
    /// The expected number of bytes to be consumed by this reader.
    /// </summary>
    int RemainingBytes { get; }

    /// <summary>
    /// Consumes a buffer.
    /// </summary>
    /// <param name="buffer">The buffer to consume.</param>
    void Apply(scoped ReadOnlySpan<byte> buffer);

    /// <summary>
    /// Gets a value indicating that reader doesn't support decoding of partial data.
    /// </summary>
    static virtual bool ThrowOnPartialData => true;
}

[StructLayout(LayoutKind.Auto)]
internal struct MemoryBlockReader(in Memory<byte> destination) : IBufferReader
{
    private Memory<byte> destination = destination;
    
    readonly int IBufferReader.RemainingBytes => destination.Length;

    void IBufferReader.Apply(ReadOnlySpan<byte> source)
        => destination = destination.Slice(source >>> destination.Span);
}

[StructLayout(LayoutKind.Auto)]
internal struct MemoryReader(in Memory<byte> destination) : IBufferReader, ISupplier<int>
{
    private Memory<byte> destination = destination;
    private int bytesWritten;

    internal readonly int BytesWritten => bytesWritten;

    readonly int IBufferReader.RemainingBytes => destination.Length;

    void IBufferReader.Apply(ReadOnlySpan<byte> source)
    {
        bytesWritten += source >>> destination.Span;
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

    public static bool IsApplicable
    {
        get
        {
            var type = typeof(T);
            return type.IsPrimitive
                   || type == typeof(UInt128)
                   || type == typeof(Int128)
                   || type == typeof(Half)
                   || type == typeof(NFloat);
        }
    }

    private Span<byte> Buffer => MemoryMarshal.CreateSpan(ref Unsafe.As<T?, byte>(ref buffer), Unsafe.SizeOf<T>());

    readonly int IBufferReader.RemainingBytes => Unsafe.SizeOf<T>() - writtenBytes;

    void IBufferReader.Apply(ReadOnlySpan<byte> source)
        => writtenBytes += source >>> Buffer.Slice(writtenBytes);

    T ISupplier<T>.Invoke() => parser(Buffer, Number.get_IsSigned<T>() is false);

    public static WellKnownIntegerReader<T> LittleEndian => new(&T.ReadLittleEndian);

    public static WellKnownIntegerReader<T> BigEndian => new(&T.ReadBigEndian);
}

[StructLayout(LayoutKind.Auto)]
internal unsafe struct IntegerReader<T>(delegate*<ReadOnlySpan<byte>, bool, T> parser) : IBufferReader, ISupplier<T>
    where T : IBinaryInteger<T>
{
    private MemoryOwner<byte> buffer = MemoryAllocator<byte>.Default.AllocateExactly(Number.get_MaxByteCount<T>());
    private int writtenBytes;

    readonly int IBufferReader.RemainingBytes => buffer.Length - writtenBytes;

    void IBufferReader.Apply(ReadOnlySpan<byte> source)
        => writtenBytes += source >>> buffer.Span.Slice(writtenBytes);

    T ISupplier<T>.Invoke()
    {
        try
        {
            return parser(buffer.Span.Slice(0, writtenBytes), Number.get_IsSigned<T>() is false);
        }
        finally
        {
            buffer.Dispose();
        }
    }

    public static IntegerReader<T> LittleEndian => new(&T.ReadLittleEndian);

    public static IntegerReader<T> BigEndian => new(&T.ReadBigEndian);
}

[StructLayout(LayoutKind.Auto)]
internal struct BinaryFormattable256Reader<T> : IBufferReader, ISupplier<T>
    where T : IBinaryFormattable<T>
{
    private Buffer256 buffer;
    private int writtenBytes;

    readonly int IBufferReader.RemainingBytes => T.Size - writtenBytes;

    void IBufferReader.Apply(ReadOnlySpan<byte> source)
    {
        Span<byte> destination = buffer;
        writtenBytes += source >>> destination.Slice(writtenBytes);
    }

    readonly T ISupplier<T>.Invoke()
    {
        ReadOnlySpan<byte> source = buffer;
        return T.Parse(source.Slice(0, writtenBytes));
    }
    
    public static unsafe bool IsApplicable => T.Size <= sizeof(Buffer256);
}

[StructLayout(LayoutKind.Auto)]
internal struct BinaryFormattableReader<T>() : IBufferReader, ISupplier<T>
    where T : IBinaryFormattable<T>
{
    private MemoryOwner<byte> buffer = MemoryAllocator<byte>.Default.AllocateExactly(T.Size);
    private int writtenBytes;

    readonly int IBufferReader.RemainingBytes => T.Size - writtenBytes;

    void IBufferReader.Apply(ReadOnlySpan<byte> source)
        => writtenBytes += source >>> buffer.Span.Slice(writtenBytes);

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

    void IBufferReader.Apply(ReadOnlySpan<byte> bytes)
    {
        remainingBytes -= bytes.Length;
        writtenChars += decoder.GetChars(bytes, buffer.Span.Slice(writtenChars), remainingBytes is 0);
    }
}

[StructLayout(LayoutKind.Auto)]
internal struct DecodingReader(Decoder decoder, int length, Memory<char> buffer) : IBufferReader, ISupplier<int>
{
    private int writtenChars, length = length;

    public readonly int RemainingBytes => Math.Min(length, buffer.Length);

    void IBufferReader.Apply(ReadOnlySpan<byte> source)
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

    void IBufferReader.Apply(ReadOnlySpan<byte> source)
    {
        Span<byte> destination = buffer;
        consumedBytes += source >>> destination.Slice(consumedBytes);
    }

    readonly TResult ISupplier<TResult>.Invoke()
    {
        ReadOnlySpan<byte> source = buffer;
        return parser(source.Slice(0, consumedBytes), arg);
    }

    public static bool IsApplicable(int length) => length <= sizeof(Buffer256);
}

[StructLayout(LayoutKind.Auto)]
internal unsafe struct ParsingReader<TArg, TResult>(TArg arg, delegate*<ReadOnlySpan<byte>, TArg, TResult> parser, int length) : IBufferReader, ISupplier<TResult>
{
    private MemoryOwner<byte> buffer = MemoryAllocator<byte>.Default.AllocateExactly(length);
    private int consumedBytes;

    readonly int IBufferReader.RemainingBytes => buffer.Length - consumedBytes;

    void IBufferReader.Apply(ReadOnlySpan<byte> source)
        => consumedBytes += source >>> buffer.Span.Slice(consumedBytes);

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
    private long length = length;
    
    readonly int IBufferReader.RemainingBytes => int.CreateSaturating(length);

    void IBufferReader.Apply(ReadOnlySpan<byte> source)
        => length -= source.Length;
}

[StructLayout(LayoutKind.Auto)]
internal struct ProxyReader<TReader>(in TReader reader) : IBufferReader, ISupplier<TReader>
    where TReader : struct, IBufferReader
{
    private TReader reader = reader;
    
    int IBufferReader.RemainingBytes => reader.RemainingBytes;

    void IBufferReader.Apply(scoped ReadOnlySpan<byte> source)
        => reader.Apply(source);

    static bool IBufferReader.ThrowOnPartialData => TReader.ThrowOnPartialData;

    readonly TReader ISupplier<TReader>.Invoke() => reader;

    public static implicit operator ProxyReader<TReader>(in TReader reader) => new(reader);
}