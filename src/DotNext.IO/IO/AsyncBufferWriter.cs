using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
#if !NETSTANDARD2_1
using System.Text.Json;
#endif
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    using Text;
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
                    writer.Write(input, token);
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
                    writer.Write(in value);
                }
                catch (Exception e)
                {
                    result = Task.FromException(e);
                }
            }

            return new ValueTask(result);
        }

        ValueTask IAsyncBinaryWriter.WriteAsync(ReadOnlyMemory<byte> input, CancellationToken token)
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
                    writer.Write(input.Span);
                }
                catch (Exception e)
                {
                    result = Task.FromException(e);
                }
            }

            return new ValueTask(result);
        }

        ValueTask IAsyncBinaryWriter.WriteAsync(ReadOnlyMemory<char> chars, EncodingContext context, StringLengthEncoding? lengthFormat, CancellationToken token)
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
                    writer.WriteString(chars.Span, in context, lengthFormat: lengthFormat);
                }
                catch (Exception e)
                {
                    result = Task.FromException(e);
                }
            }

            return new ValueTask(result);
        }

        ValueTask IAsyncBinaryWriter.WriteByteAsync(byte value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
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
                    writer.WriteByte(value, lengthFormat, in context, format, provider);
                }
                catch (Exception e)
                {
                    result = Task.FromException(e);
                }
            }

            return new ValueTask(result);
        }

        ValueTask IAsyncBinaryWriter.WriteInt16Async(short value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
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
                    writer.WriteInt16(value, lengthFormat, in context, format, provider);
                }
                catch (Exception e)
                {
                    result = Task.FromException(e);
                }
            }

            return new ValueTask(result);
        }

        ValueTask IAsyncBinaryWriter.WriteInt32Async(int value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
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
                    writer.WriteInt32(value, lengthFormat, in context, format, provider);
                }
                catch (Exception e)
                {
                    result = Task.FromException(e);
                }
            }

            return new ValueTask(result);
        }

        ValueTask IAsyncBinaryWriter.WriteInt64Async(long value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
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
                    writer.WriteInt64(value, lengthFormat, in context, format, provider);
                }
                catch (Exception e)
                {
                    result = Task.FromException(e);
                }
            }

            return new ValueTask(result);
        }

        ValueTask IAsyncBinaryWriter.WriteSingleAsync(float value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
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
                    writer.WriteSingle(value, lengthFormat, in context, format, provider);
                }
                catch (Exception e)
                {
                    result = Task.FromException(e);
                }
            }

            return new ValueTask(result);
        }

        ValueTask IAsyncBinaryWriter.WriteDoubleAsync(double value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
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
                    writer.WriteDouble(value, lengthFormat, in context, format, provider);
                }
                catch (Exception e)
                {
                    result = Task.FromException(e);
                }
            }

            return new ValueTask(result);
        }

        ValueTask IAsyncBinaryWriter.WriteDecimalAsync(decimal value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
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
                    writer.WriteDecimal(value, lengthFormat, in context, format, provider);
                }
                catch (Exception e)
                {
                    result = Task.FromException(e);
                }
            }

            return new ValueTask(result);
        }

        ValueTask IAsyncBinaryWriter.WriteDateTimeAsync(DateTime value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
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
                    writer.WriteDateTime(value, lengthFormat, in context, format, provider);
                }
                catch (Exception e)
                {
                    result = Task.FromException(e);
                }
            }

            return new ValueTask(result);
        }

        ValueTask IAsyncBinaryWriter.WriteDateTimeOffsetAsync(DateTimeOffset value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
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
                    writer.WriteDateTimeOffset(value, lengthFormat, in context, format, provider);
                }
                catch (Exception e)
                {
                    result = Task.FromException(e);
                }
            }

            return new ValueTask(result);
        }

        ValueTask IAsyncBinaryWriter.WriteGuidAsync(Guid value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, CancellationToken token)
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
                    writer.WriteGuid(value, lengthFormat, in context, format);
                }
                catch (Exception e)
                {
                    result = Task.FromException(e);
                }
            }

            return new ValueTask(result);
        }

        ValueTask IAsyncBinaryWriter.WriteTimeSpanAsync(TimeSpan value, StringLengthEncoding lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
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
                    writer.WriteTimeSpan(value, lengthFormat, in context, format, provider);
                }
                catch (Exception e)
                {
                    result = Task.FromException(e);
                }
            }

            return new ValueTask(result);
        }

        ValueTask IAsyncBinaryWriter.WriteAsync<TArg>(Action<TArg, IBufferWriter<byte>> writer, TArg arg, CancellationToken token)
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
                    writer(arg, this.writer);
                }
                catch (Exception e)
                {
                    result = Task.FromException(e);
                }
            }

            return new ValueTask(result);
        }

#if !NETSTANDARD2_1
        ValueTask IAsyncBinaryWriter.WriteJsonAsync<T>(T obj, JsonSerializerOptions? options, CancellationToken token)
        {
            Task result;
            if (token.IsCancellationRequested)
            {
                result = Task.FromCanceled(token);
            }
            else
            {
                result = Task.CompletedTask;
                Utf8JsonWriter? writer = null;
                try
                {
                    writer = new Utf8JsonWriter(this.writer, IAsyncBinaryWriter.GetWriterOptions(options));
                    JsonSerializer.Serialize(writer, obj, options);
                }
                catch (Exception e)
                {
                    result = Task.FromException(e);
                }
                finally
                {
                    writer?.Dispose();
                }
            }

            return new ValueTask(result);
        }
#endif
    }
}
