using System.Buffers;
using System.Text;

namespace DotNext.IO;

using Buffers;

internal sealed class EncodingTextWriter<TWriter> : TextBufferWriter<byte, TWriter>
    where TWriter : class, IBufferWriter<byte>
{
    private const int ConversionBufferSize = 64;

    internal EncodingTextWriter(TWriter writer, Encoding encoding, IFormatProvider? provider, Action<TWriter>? flush, Func<TWriter, CancellationToken, Task>? flushAsync)
        : base(writer, provider, flush, flushAsync)
    {
        Encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
    }

    public override Encoding Encoding { get; }

    public override void Write(ReadOnlySpan<char> chars)
        => Encoding.GetBytes(chars, writer);

    public override void Write(decimal value)
    {
        var writer = new BufferWriterSlim<char>(stackalloc char[ConversionBufferSize]);
        try
        {
            writer.WriteFormattable(value, provider: FormatProvider);
            Write(writer.WrittenSpan);
        }
        finally
        {
            writer.Dispose();
        }
    }

    public override void Write(double value)
    {
        var writer = new BufferWriterSlim<char>(stackalloc char[ConversionBufferSize]);
        try
        {
            writer.WriteFormattable(value, provider: FormatProvider);
            Write(writer.WrittenSpan);
        }
        finally
        {
            writer.Dispose();
        }
    }

    public override void Write(float value)
    {
        var writer = new BufferWriterSlim<char>(stackalloc char[ConversionBufferSize]);
        try
        {
            writer.WriteFormattable(value, provider: FormatProvider);
            Write(writer.WrittenSpan);
        }
        finally
        {
            writer.Dispose();
        }
    }

    public override void Write(int value)
    {
        var writer = new BufferWriterSlim<char>(stackalloc char[ConversionBufferSize]);
        try
        {
            writer.WriteFormattable(value, provider: FormatProvider);
            Write(writer.WrittenSpan);
        }
        finally
        {
            writer.Dispose();
        }
    }

    public override void Write(long value)
    {
        var writer = new BufferWriterSlim<char>(stackalloc char[ConversionBufferSize]);
        try
        {
            writer.WriteFormattable(value, provider: FormatProvider);
            Write(writer.WrittenSpan);
        }
        finally
        {
            writer.Dispose();
        }
    }

    public override void Write(uint value)
    {
        var writer = new BufferWriterSlim<char>(stackalloc char[ConversionBufferSize]);
        try
        {
            writer.WriteFormattable(value, provider: FormatProvider);
            Write(writer.WrittenSpan);
        }
        finally
        {
            writer.Dispose();
        }
    }

    public override void Write(ulong value)
    {
        var writer = new BufferWriterSlim<char>(stackalloc char[ConversionBufferSize]);
        try
        {
            writer.WriteFormattable(value, provider: FormatProvider);
            Write(writer.WrittenSpan);
        }
        finally
        {
            writer.Dispose();
        }
    }

    private protected override void Write(DateTime value)
    {
        var writer = new BufferWriterSlim<char>(stackalloc char[ConversionBufferSize]);
        try
        {
            writer.WriteFormattable(value, provider: FormatProvider);
            Write(writer.WrittenSpan);
        }
        finally
        {
            writer.Dispose();
        }
    }

    private protected override void Write(DateTimeOffset value)
    {
        var writer = new BufferWriterSlim<char>(stackalloc char[ConversionBufferSize]);
        try
        {
            writer.WriteFormattable(value, provider: FormatProvider);
            Write(writer.WrittenSpan);
        }
        finally
        {
            writer.Dispose();
        }
    }

    private protected override void Write(TimeSpan value)
    {
        var writer = new BufferWriterSlim<char>(stackalloc char[ConversionBufferSize]);
        try
        {
            writer.WriteFormattable(value, provider: FormatProvider);
            Write(writer.WrittenSpan);
        }
        finally
        {
            writer.Dispose();
        }
    }
}