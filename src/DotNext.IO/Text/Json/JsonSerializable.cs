using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using PipeReader = System.IO.Pipelines.PipeReader;

namespace DotNext.Text.Json;

using Buffers;
using IO;
using Runtime.Serialization;
using PipeBinaryReader = IO.Pipelines.PipeBinaryReader;

/// <summary>
/// Represents a bridge between JSON serialization framework in .NET and <see cref="ISerializable{TSelf}"/>
/// interface.
/// </summary>
/// <typeparam name="T">JSON serializable type.</typeparam>
[StructLayout(LayoutKind.Auto)]
[RequiresPreviewFeatures]
public record struct JsonSerializable<T> : ISerializable<JsonSerializable<T>>, ISupplier<T>
    where T : IJsonSerializable<T>
{
    /// <summary>
    /// Represents JSON serializable object.
    /// </summary>
    public T Value; // TODO: Change to required in C# 11

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
        else
        {
            result = writer.WriteAsync(Serialize, Value, token);
        }

        return result;

        static void Serialize(T value, IBufferWriter<byte> writer)
        {
            using var jsonWriter = new Utf8JsonWriter(writer, new JsonWriterOptions { Indented = false, SkipValidation = false });
            JsonSerializer.Serialize(jsonWriter, value, T.TypeInfo);
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
            using var buffer = MemoryAllocator.Allocate<byte>(bufferSize, exactSize: true);
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
                return new JsonSerializable<T>
                {
                    Value = (await JsonSerializer.DeserializeAsync(readerStream, T.TypeInfo).ConfigureAwait(false))!,
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
            return new JsonSerializable<T>
            {
                Value = (await JsonSerializer.DeserializeAsync(readerStream, T.TypeInfo).ConfigureAwait(false))!,
            };
        }

        // don't use PoolingAsyncValueTaskMethodBuilder because this method allocates other objects
        static async ValueTask<JsonSerializable<T>> DeserializeFromPipeAsync(PipeReader reader, CancellationToken token)
        {
            var readerStream = reader.AsStream(leaveOpen: true);
            try
            {
                return new JsonSerializable<T>
                {
                    Value = (await JsonSerializer.DeserializeAsync(readerStream, T.TypeInfo).ConfigureAwait(false))!,
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
    public override string? ToString() => Value?.ToString();
}