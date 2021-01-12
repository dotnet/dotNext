using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
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

        // TODO: Should be replaced with function pointer in C# 9
        private readonly Func<IBufferWriter<byte>, CancellationToken, Task>? flush;

        internal AsyncBufferWriter(IBufferWriter<byte> writer)
        {
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
            flush = IFlushable.TryReflectAsyncFlushMethod(writer);
        }

        private Task FlushAsync(CancellationToken token)
        {
            if (flush is null)
                return token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;
            return flush(writer, token);
        }

        Task IAsyncBinaryWriter.CopyFromAsync(Stream input, CancellationToken token)
            => input.CopyToAsync(writer, token: token);

        Task IAsyncBinaryWriter.CopyFromAsync(PipeReader input, CancellationToken token)
            => input.CopyToAsync(writer, token);

        async Task IAsyncBinaryWriter.CopyFromAsync<TArg>(Func<TArg, CancellationToken, ValueTask<ReadOnlyMemory<byte>>> supplier, TArg arg, CancellationToken token)
        {
            for (ReadOnlyMemory<byte> source; !(source = await supplier(arg, token).ConfigureAwait(false)).IsEmpty; await FlushAsync(token).ConfigureAwait(false))
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
                try
                {
                    writer.Write(value);
                    result = FlushAsync(token);
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
                try
                {
                    writer.Write(input.Span);
                    result = FlushAsync(token);
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
                try
                {
                    writer.WriteString(chars.Span, in context, lengthFormat: lengthFormat);
                    result = FlushAsync(token);
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
                try
                {
                    writer.WriteByte(value, lengthFormat, in context, format, provider);
                    result = FlushAsync(token);
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
                try
                {
                    writer.WriteInt16(value, lengthFormat, in context, format, provider);
                    result = FlushAsync(token);
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
                try
                {
                    writer.WriteInt32(value, lengthFormat, in context, format, provider);
                    result = FlushAsync(token);
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
                try
                {
                    writer.WriteInt64(value, lengthFormat, in context, format, provider);
                    result = FlushAsync(token);
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
                try
                {
                    writer.WriteSingle(value, lengthFormat, in context, format, provider);
                    result = FlushAsync(token);
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
                try
                {
                    writer.WriteDouble(value, lengthFormat, in context, format, provider);
                    result = FlushAsync(token);
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
                try
                {
                    writer.WriteDecimal(value, lengthFormat, in context, format, provider);
                    result = FlushAsync(token);
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
                try
                {
                    writer.WriteDateTime(value, lengthFormat, in context, format, provider);
                    result = FlushAsync(token);
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
                try
                {
                    writer.WriteDateTimeOffset(value, lengthFormat, in context, format, provider);
                    result = FlushAsync(token);
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
                try
                {
                    writer.WriteGuid(value, lengthFormat, in context, format);
                    result = FlushAsync(token);
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
                try
                {
                    writer.WriteTimeSpan(value, lengthFormat, in context, format, provider);
                    result = FlushAsync(token);
                }
                catch (Exception e)
                {
                    result = Task.FromException(e);
                }
            }

            return new ValueTask(result);
        }
    }
}
