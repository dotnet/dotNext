using System.Buffers;
using System.Text;
using static System.Globalization.CultureInfo;
using static System.Runtime.InteropServices.MemoryMarshal;

namespace DotNext.IO;

internal abstract class TextBufferWriter<T, TWriter> : TextWriter, IFlushable
    where T : struct, IEquatable<T>, IConvertible, IComparable<T>
    where TWriter : class, IBufferWriter<T>
{
    private protected readonly TWriter writer;
    private readonly Action<TWriter>? flush;
    private readonly Func<TWriter, CancellationToken, Task>? flushAsync;

    private protected TextBufferWriter(TWriter writer, IFormatProvider? provider, Action<TWriter>? flush, Func<TWriter, CancellationToken, Task>? flushAsync)
        : base(provider ?? InvariantCulture)
    {
        ArgumentNullException.ThrowIfNull(writer);
        this.writer = writer;
        this.flush = flush;
        this.flushAsync = flushAsync;
    }

    public sealed override void Write(bool value) => Write(value ? bool.TrueString : bool.FalseString);

    public sealed override void Write(char value) => Write(CreateReadOnlySpan(ref value, 1));

    public sealed override void Flush()
    {
        if (flush is null)
        {
            if (flushAsync is not null)
                flushAsync(writer, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        else
        {
            flush(writer);
        }
    }

    public Task FlushAsync(CancellationToken token)
    {
        if (flushAsync is null)
        {
            return flush is null ?
                Task.CompletedTask
                : Task.Factory.StartNew(() => flush(writer), token, TaskCreationOptions.DenyChildAttach, TaskScheduler.Current);
        }
        else
        {
            return flushAsync(writer, token);
        }
    }

    public sealed override Task FlushAsync() => FlushAsync(CancellationToken.None);

    public sealed override void WriteLine() => Write(new ReadOnlySpan<char>(CoreNewLine));

    public sealed override Task WriteLineAsync()
    {
        var result = Task.CompletedTask;
        try
        {
            WriteLine();
        }
        catch (Exception e)
        {
            result = Task.FromException(e);
        }

        return result;
    }

    public sealed override Task WriteLineAsync(char value)
    {
        var result = Task.CompletedTask;
        try
        {
            Write(value);
            WriteLine();
        }
        catch (Exception e)
        {
            result = Task.FromException(e);
        }

        return result;
    }

    public sealed override void Write(char[] buffer, int index, int count)
        => Write(new ReadOnlySpan<char>(buffer, index, count));

    public sealed override void Write(char[]? buffer) => Write(new ReadOnlySpan<char>(buffer));

    public sealed override void Write(string? value) => Write(value.AsSpan());

    public sealed override Task WriteLineAsync(string? value)
    {
        var result = Task.CompletedTask;
        try
        {
            Write(value);
            WriteLine();
        }
        catch (Exception e)
        {
            result = Task.FromException(e);
        }

        return result;
    }

    private protected abstract void Write(DateTime value);

    private protected abstract void Write(DateTimeOffset value);

    private protected abstract void Write(TimeSpan value);

    public sealed override void Write(object? value)
    {
        switch (value)
        {
            case null:
                break;
            case byte v:
                Write(v);
                break;
            case sbyte v:
                Write(v);
                break;
            case short v:
                Write(v);
                break;
            case ushort v:
                Write(v);
                break;
            case int v:
                Write(v);
                break;
            case uint v:
                Write(v);
                break;
            case long v:
                Write(v);
                break;
            case ulong v:
                Write(v);
                break;
            case decimal v:
                Write(v);
                break;
            case float v:
                Write(v);
                break;
            case double v:
                Write(v);
                break;
            case DateTime v:
                Write(v);
                break;
            case DateTimeOffset v:
                Write(v);
                break;
            case TimeSpan v:
                Write(v);
                break;
            case IFormattable formattable:
                Write(formattable.ToString(null, FormatProvider));
                break;
            default:
                Write(value.ToString());
                break;
        }
    }

    public sealed override void WriteLine(object? value)
    {
        Write(value);
        WriteLine();
    }

    public sealed override void WriteLine(ReadOnlySpan<char> buffer)
    {
        Write(buffer);
        WriteLine();
    }

    public sealed override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken token)
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
                Write(buffer.Span);
            }
            catch (Exception e)
            {
                result = Task.FromException(e);
            }
        }

        return result;
    }

    public sealed override Task WriteAsync(char value)
    {
        var result = Task.CompletedTask;
        try
        {
            Write(value);
        }
        catch (Exception e)
        {
            result = Task.FromException(e);
        }

        return result;
    }

    public sealed override Task WriteAsync(string? value)
    {
        var result = Task.CompletedTask;
        try
        {
            Write(value);
        }
        catch (Exception e)
        {
            result = Task.FromException(e);
        }

        return result;
    }

    public sealed override Task WriteAsync(char[] buffer, int index, int count)
        => WriteAsync(buffer.AsMemory(index, count), CancellationToken.None);

    public sealed override Task WriteLineAsync(char[] buffer, int index, int count)
    {
        var result = Task.CompletedTask;
        try
        {
            Write(buffer, index, count);
            WriteLine();
        }
        catch (Exception e)
        {
            result = Task.FromException(e);
        }

        return result;
    }

    public sealed override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken token)
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
                WriteLine(buffer.Span);
            }
            catch (Exception e)
            {
                result = Task.FromException(e);
            }
        }

        return result;
    }

    public override void Write(StringBuilder? sb)
    {
        foreach (var chunk in sb?.GetChunks() ?? new())
            Write(chunk.Span);
    }

    public override void WriteLine(StringBuilder? sb)
    {
        Write(sb);
        WriteLine();
    }

    public override Task WriteAsync(StringBuilder? value, CancellationToken token)
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
                Write(value);
            }
            catch (Exception e)
            {
                result = Task.FromException(e);
            }
        }

        return result;
    }

    public override Task WriteLineAsync(StringBuilder? value, CancellationToken token)
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
                WriteLine(value);
            }
            catch (Exception e)
            {
                result = Task.FromException(e);
            }
        }

        return result;
    }
}