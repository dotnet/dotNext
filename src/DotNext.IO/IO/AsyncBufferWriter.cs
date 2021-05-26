using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Numerics;
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
#if NETSTANDARD2_1
                result = new (Task.FromCanceled(token));
#else
                result = ValueTask.FromCanceled(token);
#endif
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
#if NETSTANDARD2_1
                    result = new (Task.FromException(e));
#else
                    result = ValueTask.FromException(e);
#endif
                }
            }

            return result;
        }

        ValueTask IAsyncBinaryWriter.WriteAsync(ReadOnlyMemory<byte> input, LengthFormat? lengthFormat, CancellationToken token)
        {
            ValueTask result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled(token));
#else
                result = ValueTask.FromCanceled(token);
#endif
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
#if NETSTANDARD2_1
                    result = new (Task.FromException(e));
#else
                    result = ValueTask.FromException(e);
#endif
                }
            }

            return result;
        }

        ValueTask IAsyncBinaryWriter.WriteBigIntegerAsync(BigInteger value, bool littleEndian, LengthFormat? lengthFormat, CancellationToken token)
        {
            ValueTask result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled(token));
#else
                result = ValueTask.FromCanceled(token);
#endif
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
#if NETSTANDARD2_1
                    result = new (Task.FromException(e));
#else
                    result = ValueTask.FromException(e);
#endif
                }
            }

            return result;
        }

        ValueTask ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<byte> input, CancellationToken token)
        {
            ValueTask result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled(token));
#else
                result = ValueTask.FromCanceled(token);
#endif
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
#if NETSTANDARD2_1
                    result = new (Task.FromException(e));
#else
                    result = ValueTask.FromException(e);
#endif
                }
            }

            return result;
        }

        ValueTask IAsyncBinaryWriter.WriteAsync(ReadOnlyMemory<char> chars, EncodingContext context, LengthFormat? lengthFormat, CancellationToken token)
        {
            ValueTask result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled(token));
#else
                result = ValueTask.FromCanceled(token);
#endif
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
#if NETSTANDARD2_1
                    result = new (Task.FromException(e));
#else
                    result = ValueTask.FromException(e);
#endif
                }
            }

            return result;
        }

        ValueTask IAsyncBinaryWriter.WriteByteAsync(byte value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
        {
            ValueTask result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled(token));
#else
                result = ValueTask.FromCanceled(token);
#endif
            }
            else
            {
                result = new();
                try
                {
                    writer.WriteByte(value, lengthFormat, in context, format, provider);
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException(e));
#else
                    result = ValueTask.FromException(e);
#endif
                }
            }

            return result;
        }

        ValueTask IAsyncBinaryWriter.WriteInt16Async(short value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
        {
            ValueTask result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled(token));
#else
                result = ValueTask.FromCanceled(token);
#endif
            }
            else
            {
                result = new();
                try
                {
                    writer.WriteInt16(value, lengthFormat, in context, format, provider);
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException(e));
#else
                    result = ValueTask.FromException(e);
#endif
                }
            }

            return result;
        }

        ValueTask IAsyncBinaryWriter.WriteInt32Async(int value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
        {
            ValueTask result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled(token));
#else
                result = ValueTask.FromCanceled(token);
#endif
            }
            else
            {
                result = new();
                try
                {
                    writer.WriteInt32(value, lengthFormat, in context, format, provider);
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException(e));
#else
                    result = ValueTask.FromException(e);
#endif
                }
            }

            return result;
        }

        ValueTask IAsyncBinaryWriter.WriteInt64Async(long value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
        {
            ValueTask result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled(token));
#else
                result = ValueTask.FromCanceled(token);
#endif
            }
            else
            {
                result = new();
                try
                {
                    writer.WriteInt64(value, lengthFormat, in context, format, provider);
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException(e));
#else
                    result = ValueTask.FromException(e);
#endif
                }
            }

            return result;
        }

        ValueTask IAsyncBinaryWriter.WriteSingleAsync(float value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
        {
            ValueTask result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled(token));
#else
                result = ValueTask.FromCanceled(token);
#endif
            }
            else
            {
                result = new();
                try
                {
                    writer.WriteSingle(value, lengthFormat, in context, format, provider);
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException(e));
#else
                    result = ValueTask.FromException(e);
#endif
                }
            }

            return result;
        }

        ValueTask IAsyncBinaryWriter.WriteDoubleAsync(double value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
        {
            ValueTask result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled(token));
#else
                result = ValueTask.FromCanceled(token);
#endif
            }
            else
            {
                result = new();
                try
                {
                    writer.WriteDouble(value, lengthFormat, in context, format, provider);
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException(e));
#else
                    result = ValueTask.FromException(e);
#endif
                }
            }

            return result;
        }

        ValueTask IAsyncBinaryWriter.WriteDecimalAsync(decimal value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
        {
            ValueTask result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled(token));
#else
                result = ValueTask.FromCanceled(token);
#endif
            }
            else
            {
                result = new();
                try
                {
                    writer.WriteDecimal(value, lengthFormat, in context, format, provider);
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException(e));
#else
                    result = ValueTask.FromException(e);
#endif
                }
            }

            return result;
        }

        ValueTask IAsyncBinaryWriter.WriteDateTimeAsync(DateTime value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
        {
            ValueTask result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled(token));
#else
                result = ValueTask.FromCanceled(token);
#endif
            }
            else
            {
                result = new();
                try
                {
                    writer.WriteDateTime(value, lengthFormat, in context, format, provider);
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException(e));
#else
                    result = ValueTask.FromException(e);
#endif
                }
            }

            return result;
        }

        ValueTask IAsyncBinaryWriter.WriteDateTimeOffsetAsync(DateTimeOffset value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
        {
            ValueTask result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled(token));
#else
                result = ValueTask.FromCanceled(token);
#endif
            }
            else
            {
                result = new();
                try
                {
                    writer.WriteDateTimeOffset(value, lengthFormat, in context, format, provider);
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException(e));
#else
                    result = ValueTask.FromException(e);
#endif
                }
            }

            return result;
        }

        ValueTask IAsyncBinaryWriter.WriteGuidAsync(Guid value, LengthFormat lengthFormat, EncodingContext context, string? format, CancellationToken token)
        {
            ValueTask result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled(token));
#else
                result = ValueTask.FromCanceled(token);
#endif
            }
            else
            {
                result = new();
                try
                {
                    writer.WriteGuid(value, lengthFormat, in context, format);
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException(e));
#else
                    result = ValueTask.FromException(e);
#endif
                }
            }

            return result;
        }

        ValueTask IAsyncBinaryWriter.WriteTimeSpanAsync(TimeSpan value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
        {
            ValueTask result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled(token));
#else
                result = ValueTask.FromCanceled(token);
#endif
            }
            else
            {
                result = new();
                try
                {
                    writer.WriteTimeSpan(value, lengthFormat, in context, format, provider);
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException(e));
#else
                    result = ValueTask.FromException(e);
#endif
                }
            }

            return result;
        }

        ValueTask IAsyncBinaryWriter.WriteBigIntegerAsync(BigInteger value, LengthFormat lengthFormat, EncodingContext context, string? format, IFormatProvider? provider, CancellationToken token)
        {
            ValueTask result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled(token));
#else
                result = ValueTask.FromCanceled(token);
#endif
            }
            else
            {
                result = new();
                try
                {
                    writer.WriteBigInteger(value, lengthFormat, in context, format, provider);
                }
                catch (Exception e)
                {
#if NETSTANDARD2_1
                    result = new (Task.FromException(e));
#else
                    result = ValueTask.FromException(e);
#endif
                }
            }

            return result;
        }

        ValueTask IAsyncBinaryWriter.WriteAsync<TArg>(Action<TArg, IBufferWriter<byte>> writer, TArg arg, CancellationToken token)
        {
            ValueTask result;
            if (token.IsCancellationRequested)
            {
#if NETSTANDARD2_1
                result = new (Task.FromCanceled(token));
#else
                result = ValueTask.FromCanceled(token);
#endif
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
#if NETSTANDARD2_1
                    result = new (Task.FromException(e));
#else
                    result = ValueTask.FromException(e);
#endif
                }
            }

            return result;
        }

        IBufferWriter<byte>? IAsyncBinaryWriter.TryGetBufferWriter() => writer;
    }
}
