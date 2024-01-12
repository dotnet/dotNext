using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.InteropServices;

namespace DotNext.IO;

using Buffers;
using Pipelines;
using Text;

[StructLayout(LayoutKind.Auto)]
internal readonly struct AsyncBufferWriter(IBufferWriter<byte> writer) : IAsyncBinaryWriter
{
    internal Stream AsStream() => StreamSource.AsStream(writer);

    Memory<byte> IAsyncBinaryWriter.Buffer => writer.GetMemory();

    ValueTask IAsyncBinaryWriter.AdvanceAsync(int bytesWritten, CancellationToken token)
    {
        switch (bytesWritten)
        {
            case < 0:
                return ValueTask.FromException(new ArgumentOutOfRangeException(nameof(bytesWritten)));
            case 0:
                return ValueTask.CompletedTask;
            case > 0:
                writer.Advance(bytesWritten);
                goto case 0;
        }
    }

    ValueTask IAsyncBinaryWriter.CopyFromAsync(Stream source, long? count, CancellationToken token)
        => count.HasValue ? source.CopyToAsync(writer, count.GetValueOrDefault(), token: token) : source.CopyToAsync(writer, token: token);

    ValueTask IAsyncBinaryWriter.CopyFromAsync(PipeReader source, long? count, CancellationToken token)
    {
        var consumer = new BufferConsumer<byte>(writer);
        return count.HasValue
            ? source.CopyToAsync(consumer, count.GetValueOrDefault(), token)
            : source.CopyToAsync(consumer, token);
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
            result = ValueTask.CompletedTask;
            try
            {
                writer.Write(value);
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }

        return result;
    }

    ValueTask IAsyncBinaryWriter.WriteLittleEndianAsync<T>(T value, CancellationToken token)
    {
        ValueTask result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled(token);
        }
        else
        {
            result = ValueTask.CompletedTask;
            try
            {
                writer.WriteLittleEndian(value);
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }

        return result;
    }

    ValueTask IAsyncBinaryWriter.WriteBigEndianAsync<T>(T value, CancellationToken token)
    {
        ValueTask result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled(token);
        }
        else
        {
            result = ValueTask.CompletedTask;
            try
            {
                writer.WriteBigEndian(value);
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
            result = ValueTask.CompletedTask;
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

    ValueTask ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<byte> input, CancellationToken token)
    {
        ValueTask result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled(token);
        }
        else
        {
            result = ValueTask.CompletedTask;
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

    ValueTask<long> IAsyncBinaryWriter.EncodeAsync(ReadOnlyMemory<char> chars, EncodingContext context, LengthFormat? lengthFormat, CancellationToken token)
    {
        ValueTask<long> result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled<long>(token);
        }
        else
        {
            try
            {
                result = new(writer.Encode(chars.Span, in context, lengthFormat: lengthFormat));
            }
            catch (Exception e)
            {
                result = ValueTask.FromException<long>(e);
            }
        }

        return result;
    }

    ValueTask<long> IAsyncBinaryWriter.FormatAsync<T>(T value, EncodingContext context, LengthFormat? lengthFormat, string? format, IFormatProvider? provider, MemoryAllocator<char>? allocator, CancellationToken token)
    {
        ValueTask<long> result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled<long>(token);
        }
        else
        {
            try
            {
                result = new(writer.Format(value, in context, lengthFormat, format, provider, allocator));
            }
            catch (Exception e)
            {
                result = ValueTask.FromException<long>(e);
            }
        }

        return result;
    }

    ValueTask<int> IAsyncBinaryWriter.FormatAsync<T>(T value, LengthFormat? lengthFormat, string? format, IFormatProvider? provider, CancellationToken token)
    {
        ValueTask<int> result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled<int>(token);
        }
        else
        {
            try
            {
                result = new(writer.Format(value, lengthFormat, format, provider));
            }
            catch (Exception e)
            {
                result = ValueTask.FromException<int>(e);
            }
        }

        return result;
    }

    IBufferWriter<byte>? IAsyncBinaryWriter.TryGetBufferWriter() => writer;
}