﻿using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
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
                    writer.Write(input);
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

        ValueTask IAsyncBinaryWriter.WriteAsync(ReadOnlyMemory<byte> input, LengthFormat? lengthFormat, CancellationToken token)
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
                    if (lengthFormat.HasValue)
                        writer.WriteLength(input.Length, lengthFormat.GetValueOrDefault());

                    writer.Write(input.Span);
                }
                catch (Exception e)
                {
                    result = Task.FromException(e);
                }
            }

            return new ValueTask(result);
        }

        ValueTask ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<byte> input, CancellationToken token)
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

        ValueTask IAsyncBinaryWriter.WriteAsync(ReadOnlyMemory<char> chars, EncodingContext context, LengthFormat? lengthFormat, CancellationToken token)
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

        ValueTask IAsyncBinaryWriter.WriteByteAsync(byte value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
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

        ValueTask IAsyncBinaryWriter.WriteInt16Async(short value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
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

        ValueTask IAsyncBinaryWriter.WriteInt32Async(int value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
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

        ValueTask IAsyncBinaryWriter.WriteInt64Async(long value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
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

        ValueTask IAsyncBinaryWriter.WriteSingleAsync(float value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
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

        ValueTask IAsyncBinaryWriter.WriteDoubleAsync(double value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
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

        ValueTask IAsyncBinaryWriter.WriteDecimalAsync(decimal value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
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

        ValueTask IAsyncBinaryWriter.WriteDateTimeAsync(DateTime value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
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

        ValueTask IAsyncBinaryWriter.WriteDateTimeOffsetAsync(DateTimeOffset value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
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

        ValueTask IAsyncBinaryWriter.WriteGuidAsync(Guid value, LengthFormat lengthFormat, EncodingContext context, string? format, CancellationToken token)
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

        ValueTask IAsyncBinaryWriter.WriteTimeSpanAsync(TimeSpan value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
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
    }
}
