using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace DotNext.Text.Json;

using Buffers;
using IO;
using IO.Pipelines;
using Runtime.Serialization;

/// <summary>
/// Represents a bridge between JSON serialization framework in .NET and <see cref="ISerializable{TSelf}"/>
/// interface.
/// </summary>
/// <typeparam name="T">JSON serializable type.</typeparam>
[StructLayout(LayoutKind.Auto)]
public record struct JsonSerializable<T> : ISerializable<JsonSerializable<T>>, ISupplier<T>
    where T : IJsonSerializable<T>
{
    /// <summary>
    /// Represents JSON serializable object.
    /// </summary>
    required public T Value;

    /// <inheritdoc />
    readonly T ISupplier<T>.Invoke() => Value;

    /// <inheritdoc />
    readonly long? IDataTransferObject.Length => null;

    /// <inheritdoc />
    readonly ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
    {
        ValueTask result;
        var buffer = writer.TryGetBufferWriter();

        if (buffer is not null)
        {
            // fast path - synchronous serialization
            result = ValueTask.CompletedTask;
            try
            {
                Serialize(Value, buffer);
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }
        else if (typeof(TWriter) == typeof(AsyncStreamBinaryAccessor))
        {
            result = new(JsonSerializer.SerializeAsync(Unsafe.As<TWriter, AsyncStreamBinaryAccessor>(ref writer).Stream, Value, T.TypeInfo, token));
        }
        else if (typeof(TWriter) == typeof(PipeBinaryWriter))
        {
            result = SerializeToStreamAsync(Unsafe.As<TWriter, PipeBinaryWriter>(ref writer).Writer.AsStream(leaveOpen: true), Value, token);
        }
        else
        {
            result = SerializeToStreamAsync(StreamSource.AsAsynchronousStream(new Wrapper<TWriter>(writer)), Value, token);
        }

        return result;

        static void Serialize(T value, IBufferWriter<byte> writer)
        {
            using var jsonWriter = new Utf8JsonWriter(writer, new JsonWriterOptions { Indented = false, SkipValidation = false });
            JsonSerializer.Serialize(jsonWriter, value, T.TypeInfo);
        }

        static async ValueTask SerializeToStreamAsync(Stream stream, T value, CancellationToken token)
        {
            try
            {
                await JsonSerializer.SerializeAsync(stream, value, T.TypeInfo, token).ConfigureAwait(false);
            }
            finally
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc cref="ISerializable{TSelf}.ReadFromAsync{TReader}(TReader, CancellationToken)"/>
    public static ValueTask<JsonSerializable<T>> ReadFromAsync<TReader>(TReader reader, CancellationToken token = default)
        where TReader : notnull, IAsyncBinaryReader
    {
        ValueTask<JsonSerializable<T>> result;

        if (reader.TryGetSequence(out var buffer))
        {
            // fast path - synchronous deserialization
            try
            {
                result = new(Deserialize(buffer));
            }
            catch (Exception e)
            {
                result = ValueTask.FromException<JsonSerializable<T>>(e);
            }
        }
        else if (typeof(TReader) == typeof(AsyncStreamBinaryAccessor))
        {
            result = DeserializeFromStreamAsync(Unsafe.As<TReader, AsyncStreamBinaryAccessor>(ref reader).Stream, token);
        }
        else if (typeof(TReader) == typeof(PipeBinaryReader))
        {
            result = DeserializeFromPipeAsync(Unsafe.As<TReader, PipeBinaryReader>(ref reader).Reader, token);
        }
        else if (reader.TryGetRemainingBytesCount(out var bufferSize) && bufferSize <= Array.MaxLength)
        {
            // slow path, but still nice - we can pre-allocate buffer
            result = DeserializeBufferedAsync(reader, (int)bufferSize, token);
        }
        else
        {
            // slow path, async I/O and memory allocation
            result = DeserializeAsync(reader, token);
        }

        return result;

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        static async ValueTask<JsonSerializable<T>> DeserializeBufferedAsync(TReader reader, int bufferSize, CancellationToken token)
        {
            using var buffer = Memory.AllocateExactly<byte>(bufferSize);
            await reader.ReadAsync(buffer.Memory, token).ConfigureAwait(false);
            return Deserialize(new(buffer.Memory));
        }

        // don't use PoolingAsyncValueTaskMethodBuilder because this method allocates other objects
        static async ValueTask<JsonSerializable<T>> DeserializeAsync(TReader reader, CancellationToken token)
        {
            var buffer = new FileBufferingWriter(initialCapacity: 4096);
            var readerStream = default(Stream);
            try
            {
                await reader.CopyToAsync(buffer.As<Stream>(), token).ConfigureAwait(false);

                readerStream = await buffer.GetWrittenContentAsStreamAsync(token).ConfigureAwait(false);
                return new()
                {
                    Value = (await JsonSerializer.DeserializeAsync(readerStream, T.TypeInfo, token).ConfigureAwait(false))!,
                };
            }
            finally
            {
                if (readerStream is not null)
                    await readerStream.DisposeAsync().ConfigureAwait(false);

                await buffer.DisposeAsync().ConfigureAwait(false);
            }
        }

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        static async ValueTask<JsonSerializable<T>> DeserializeFromStreamAsync(Stream readerStream, CancellationToken token)
        {
            return new()
            {
                Value = (await JsonSerializer.DeserializeAsync(readerStream, T.TypeInfo, token).ConfigureAwait(false))!,
            };
        }

        // don't use PoolingAsyncValueTaskMethodBuilder because this method allocates other objects
        static async ValueTask<JsonSerializable<T>> DeserializeFromPipeAsync(PipeReader reader, CancellationToken token)
        {
            var readerStream = reader.AsStream(leaveOpen: true);
            try
            {
                return new()
                {
                    Value = (await JsonSerializer.DeserializeAsync(readerStream, T.TypeInfo, token).ConfigureAwait(false))!,
                };
            }
            finally
            {
                await readerStream.DisposeAsync().ConfigureAwait(false);
            }
        }

        static JsonSerializable<T> Deserialize(ReadOnlySequence<byte> buffer)
        {
            var jsonReader = new Utf8JsonReader(buffer);
            return new() { Value = JsonSerializer.Deserialize(ref jsonReader, T.TypeInfo)! };
        }
    }

    /// <inheritdoc />
    public override readonly string? ToString() => Value?.ToString();

    private readonly struct Wrapper<TWriter>(TWriter writer) : ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>, IFlushable
        where TWriter : notnull, IAsyncBinaryWriter
    {
        ValueTask ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<byte> source, CancellationToken token)
            => writer.Invoke(source, token);

        void IFlushable.Flush()
        {
        }

        Task IFlushable.FlushAsync(CancellationToken token)
            => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;
    }
}