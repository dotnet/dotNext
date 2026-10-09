using System.Runtime.CompilerServices;
using System.Text;
using DotNext.Buffers;

namespace DotNext.IO;

using Buffers.Text;
using Text;

/// <summary>
/// Represents a modern base class for custom text writers that requires only necessary
/// abstract methods to work correctly.
/// </summary>
public abstract class ModernTextWriter : TextWriter, IFlushable
{
    /// <summary>
    /// Initializes a new text writer with the specified format provider.
    /// </summary>
    /// <param name="formatProvider">An object that controls formatting.</param>
    protected ModernTextWriter(IFormatProvider? formatProvider)
        : base(formatProvider)
    {
    }
    
    /// <inheritdoc/>
    public abstract override void Write(ReadOnlySpan<char> chars);

    /// <summary>
    /// Writes formattable value synchronously.
    /// </summary>
    /// <param name="value">Formattable value.</param>
    protected virtual void WriteValue<TFormattable>(TFormattable value)
        where TFormattable : ISpanFormattable
    {
        var writer = new BufferWriterSlim<char>(stackalloc char[64]);

        try
        {
            writer.Format(value, provider: FormatProvider);
            Write(writer.WrittenSpan);
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <inheritdoc/>
    public override void Write(bool value) => Write(value ? bool.TrueString : bool.FalseString);

    /// <inheritdoc/>
    public sealed override void Write(char value) => Write(new ReadOnlySpan<char>(in value));
    
    /// <inheritdoc/>
    public sealed override Task FlushAsync() => FlushAsync(CancellationToken.None);

    /// <inheritdoc/>
    public sealed override void WriteLine() => Write(new ReadOnlySpan<char>(CoreNewLine));
    
    /// <inheritdoc/>
    public sealed override void Write(char[] buffer, int index, int count)
        => Write(new ReadOnlySpan<char>(buffer, index, count));
    
    /// <inheritdoc/>
    public sealed override void Write(char[]? buffer) => Write(new ReadOnlySpan<char>(buffer));

    /// <inheritdoc/>
    public sealed override void Write(string? value) => Write(value.AsSpan());
    
    /// <inheritdoc/>
    public override void Write(StringBuilder? sb)
    {
        foreach (var chunk in sb?.GetChunks() ?? new())
            Write(chunk.Span);
    }
    
    /// <inheritdoc/>
    public sealed override void WriteLine(object? value)
    {
        Write(value);
        WriteLine();
    }

    /// <inheritdoc/>
    public sealed override void WriteLine(ReadOnlySpan<char> buffer)
    {
        Write(buffer);
        WriteLine();
    }

    /// <inheritdoc/>
    public sealed override void Write(object? value)
    {
        switch (value)
        {
            case string str:
                Write(str.AsSpan());
                break;
            case StringBuilder sb:
                Write(sb);
                break;
            case ISpanFormattable formattable:
                WriteValue(formattable);
                break;
            case IFormattable formattable:
                Write(formattable.ToString(format: null, formatProvider: FormatProvider).AsSpan());
                break;
            case not null:
                Write(value.ToString().AsSpan());
                break;
        }
    }

    /// <inheritdoc/>
    public override void Write(string format, params ReadOnlySpan<object?> arg)
    {
        var composite = CompositeFormat.IsInterned(format) ?? CompositeFormat.Parse(format);
        var writer = new BufferWriterSlim<char>(stackalloc char[128]);
        try
        {
            writer.Format(composite, arg, FormatProvider);
            Write(writer.WrittenSpan);
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <inheritdoc/>
    public sealed override void WriteLine(string? value)
    {
        Write(value);
        WriteLine();
    }

    /// <inheritdoc/>
    public sealed override void WriteLine(bool value)
    {
        Write(value);
        WriteLine();
    }

    /// <inheritdoc/>
    public override void WriteLine(char value)
    {
        Write(value);
        WriteLine();
    }

    /// <inheritdoc/>
    public override void WriteLine(char[]? buffer)
    {
        Write(buffer.AsSpan());
        WriteLine();
    }

    /// <inheritdoc/>
    public sealed override void WriteLine(char[] buffer, int index, int count)
        => WriteLine(new ReadOnlySpan<char>(buffer, index, count));

    /// <inheritdoc/>
    public sealed override void WriteLine(decimal value)
    {
        Write(value);
        WriteLine();
    }

    /// <inheritdoc/>
    public sealed override void WriteLine(double value)
    {
        Write(value);
        WriteLine();
    }

    /// <inheritdoc/>
    public sealed override void WriteLine(int value)
    {
        Write(value);
        WriteLine();
    }

    /// <inheritdoc/>
    public sealed override void WriteLine(long value)
    {
        Write(value);
        WriteLine();
    }

    /// <inheritdoc/>
    public sealed override void WriteLine(float value)
    {
        Write(value);
        WriteLine();
    }

    /// <inheritdoc/>
    [CLSCompliant(false)]
    public override void WriteLine(uint value)
    {
        Write(value);
        WriteLine();
    }

    /// <inheritdoc/>
    [CLSCompliant(false)]
    public override void WriteLine(ulong value)
    {
        Write(value);
        WriteLine();
    }

    /// <inheritdoc/>
    public sealed override void WriteLine(string format, object? arg0)
    {
        Write(format, arg0);
        WriteLine();
    }

    /// <inheritdoc/>
    public sealed override void WriteLine(string format, object? arg0, object? arg1)
    {
        Write(format, arg0, arg1);
        WriteLine();
    }

    /// <inheritdoc/>
    public sealed override void WriteLine(string format, object? arg0, object? arg1, object? arg2)
    {
        Write(format, arg0, arg1, arg2);
        WriteLine();
    }

    /// <inheritdoc/>
    public sealed override void WriteLine(string format, params object?[] arg)
    {
        Write(format, arg);
        WriteLine();
    }

    /// <inheritdoc/>
    public sealed override void WriteLine(string format, params ReadOnlySpan<object?> arg)
    {
        Write(format, arg);
        WriteLine();
    }

    /// <inheritdoc/>
    public override void WriteLine(StringBuilder? value)
    {
        Write(value);
        WriteLine();
    }

    /// <inheritdoc/>
    public sealed override void Write(decimal value) => WriteValue(value);

    /// <inheritdoc/>
    public sealed override void Write(double value) => WriteValue(value);

    /// <inheritdoc/>
    public sealed override void Write(int value) => WriteValue(value);

    /// <inheritdoc/>
    public sealed override void Write(long value) => WriteValue(value);

    /// <inheritdoc/>
    public sealed override void Write(float value) => WriteValue(value);
    
    /// <inheritdoc/>
    [CLSCompliant(false)]
    public sealed override void Write(uint value) => WriteValue(value);

    /// <inheritdoc/>
    [CLSCompliant(false)]
    public override void Write(ulong value) => WriteValue(value);

    /// <inheritdoc/>
    public sealed override void Write(string format, object? arg0)
        => Write(format, new ReadOnlySpan<object?>(in arg0));

    /// <inheritdoc/>
    public sealed override void Write(string format, object? arg0, object? arg1)
    {
        var args = new InlineArray2<object?>();
        args[0] = arg0;
        args[1] = arg1;

        Write(format, (ReadOnlySpan<object?>)args);
    }

    /// <inheritdoc/>
    public sealed override void Write(string format, object? arg0, object? arg1, object? arg2)
    {
        var args = new InlineArray3<object?>();
        args[0] = arg0;
        args[1] = arg1;
        args[2] = arg2;

        Write(format, (ReadOnlySpan<object?>)args);
    }

    /// <inheritdoc/>
    public sealed override void Write(string format, params object?[] arg)
        => Write(format, new ReadOnlySpan<object?>(arg));

    /// <inheritdoc/>
    public sealed override Task WriteAsync(char[] buffer, int index, int count)
        => WriteAsync(buffer.AsMemory(index, count), CancellationToken.None);

    /// <inheritdoc/>
    public abstract override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken token = default);

    /// <inheritdoc/>
    public override Task WriteAsync(string? value)
        => WriteAsync(value.AsMemory(), CancellationToken.None);

    /// <inheritdoc/>
    public sealed override Task WriteLineAsync() => WriteLineAsync(CancellationToken.None);
    
    /// <inheritdoc/>
    public sealed override Task WriteLineAsync(string? value)
        => WriteLineAsync(value.AsMemory(), CancellationToken.None);

    /// <inheritdoc/>
    public sealed override Task WriteLineAsync(char[] buffer, int index, int count)
        => WriteLineAsync(new ReadOnlyMemory<char>(buffer, index, count), CancellationToken.None);

    /// <inheritdoc/>
    public override Task WriteAsync(StringBuilder? value, CancellationToken token = default)
        => value is null ? Task.CompletedTask : WriteCoreAsync(value, token);

    private async Task WriteCoreAsync(StringBuilder builder, CancellationToken token)
    {
        foreach (var chunk in builder.GetChunks())
        {
            await WriteAsync(chunk, token).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public override Task WriteAsync(char value) => WriteAsync([value]);

    /// <inheritdoc/>
    public override Task WriteLineAsync(char value) => WriteLineAsync([value]);

    private Task WriteLineAsync(CancellationToken token) => WriteAsync(CoreNewLine, token);

    /// <inheritdoc/>
    public override Task WriteLineAsync(StringBuilder? value, CancellationToken token = default)
        => value is null ? WriteLineAsync(token) : WriteLineCoreAsync(value, token);

    private async Task WriteLineCoreAsync(StringBuilder builder, CancellationToken token)
    {
        await WriteCoreAsync(builder, token).ConfigureAwait(false);
        await WriteLineAsync(token).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken token = default)
    {
        await WriteAsync(buffer, token).ConfigureAwait(false);
        await WriteLineAsync(token).ConfigureAwait(false);
    }
}