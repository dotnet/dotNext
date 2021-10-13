using System.Buffers;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DotNext.IO;

using Text;
using static Buffers.BufferHelpers;
using static Buffers.BufferWriter;
using static Pipelines.PipeExtensions;

[StructLayout(LayoutKind.Auto)]
internal readonly struct AsyncBufferWriter : IAsyncBinaryWriter
{
    private readonly IBufferWriter<byte> writer;

    internal AsyncBufferWriter(IBufferWriter<byte> writer)
    {
        this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    Task IAsyncBinaryWriter.CopyFromAsync(Stream input, CancellationToken token)
        => input.CopyToAsync(writer, token: token);

    Task IAsyncBinaryWriter.CopyFromAsync(PipeReader input, CancellationToken token)
        => input.CopyToAsync(writer, token);

    async Task IAsyncBinaryWriter.CopyFromAsync<TArg>(Func<TArg, CancellationToken, ValueTask<ReadOnlyMemory<byte>>> supplier, TArg arg, CancellationToken token)
    {
        for (ReadOnlyMemory<byte> source; !(source = await supplier(arg, token).ConfigureAwait(false)).IsEmpty; token.ThrowIfCancellationRequested())
            writer.Write(source.Span);
    }

    Task IAsyncBinaryWriter.WriteAsync(ReadOnlySequence<byte> input, CancellationToken token)
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
                writer.Write(in input);
            }
            catch (Exception e)
            {
                result = Task.FromException(e);
            }
        }

        return result;
    }

    ValueTask IAsyncBinaryWriter.WriteAsync<T>(T value, CancellationToken token)
    {
        ValueTask result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled(token);
        }
        else
        {
            result = new();
            try
            {
                writer.Write(in value);
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }

        return result;
    }

    ValueTask IAsyncBinaryWriter.WriteAsync(ReadOnlyMemory<byte> input, LengthFormat? lengthFormat, CancellationToken token)
    {
        ValueTask result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled(token);
        }
        else
        {
            result = new();
            try
            {
                if (lengthFormat.HasValue)
                    writer.WriteLength(input.Length, lengthFormat.GetValueOrDefault());

                writer.Write(input.Span);
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }

        return result;
    }

    ValueTask IAsyncBinaryWriter.WriteBigIntegerAsync(BigInteger value, bool littleEndian, LengthFormat? lengthFormat, CancellationToken token)
    {
        ValueTask result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled(token);
        }
        else
        {
            result = new();
            try
            {
                writer.WriteBigInteger(in value, littleEndian, lengthFormat);
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }

        return result;
    }

    ValueTask ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<byte> input, CancellationToken token)
    {
        ValueTask result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled(token);
        }
        else
        {
            result = new();
            try
            {
                writer.Write(input.Span);
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }

        return result;
    }

    ValueTask IAsyncBinaryWriter.WriteStringAsync(ReadOnlyMemory<char> chars, EncodingContext context, LengthFormat? lengthFormat, CancellationToken token)
    {
        ValueTask result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled(token);
        }
        else
        {
            result = new();
            try
            {
                writer.WriteString(chars.Span, in context, lengthFormat: lengthFormat);
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }

        return result;
    }

    ValueTask IAsyncBinaryWriter.WriteFormattableAsync<T>(T value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
    {
        ValueTask result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled(token);
        }
        else
        {
            result = new();
            try
            {
                writer.WriteFormattable(value, lengthFormat, in context, format, provider);
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }

        return result;
    }

    [RequiresPreviewFeatures]
    ValueTask IAsyncBinaryWriter.WriteFormattableAsync<T>(T value, CancellationToken token)
    {
        ValueTask result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled(token);
        }
        else
        {
            result = new();
            try
            {
                writer.WriteFormattable(value);
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }

        return result;
    }

    ValueTask IAsyncBinaryWriter.WriteAsync<TArg>(Action<TArg, IBufferWriter<byte>> writer, TArg arg, CancellationToken token)
    {
        ValueTask result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled(token);
        }
        else
        {
            result = new();
            try
            {
                writer(arg, this.writer);
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }

        return result;
    }

    IBufferWriter<byte>? IAsyncBinaryWriter.TryGetBufferWriter() => writer;
}