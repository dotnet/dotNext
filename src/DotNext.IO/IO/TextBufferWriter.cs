using System.Buffers;
using System.Text;
using static System.Globalization.CultureInfo;

namespace DotNext.IO;

internal abstract class TextBufferWriter<T, TWriter> : ModernTextWriter
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

    public sealed override void Flush()
    {
        if (flush is null)
        {
            flushAsync?.Invoke(writer, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        else
        {
            flush(writer);
        }
    }

    public override Task FlushAsync(CancellationToken token)
    {
        if (flushAsync is null)
        {
            return flush is null
                ? Task.CompletedTask
                : Task.Factory.StartNew(() => flush(writer), token, TaskCreationOptions.DenyChildAttach, TaskScheduler.Current);
        }

        return flushAsync(writer, token);
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

    public sealed override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken token = default)
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

    public sealed override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken token = default)
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

    public sealed override Task WriteAsync(StringBuilder? value, CancellationToken token = default)
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

    public sealed override Task WriteLineAsync(StringBuilder? value, CancellationToken token = default)
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