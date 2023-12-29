using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace DotNext.Text.Json;

using IO;
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
        else
        {
            var stream = IAsyncBinaryWriter.GetStream(writer, out var keepAlive);

            result = keepAlive
                ? new(JsonSerializer.SerializeAsync(stream, Value, T.TypeInfo, token))
                : SerializeAsync(stream, Value, token);
        }

        return result;

        static void Serialize(T value, IBufferWriter<byte> writer)
        {
            using var jsonWriter = new Utf8JsonWriter(writer, new JsonWriterOptions { Indented = false, SkipValidation = false });
            JsonSerializer.Serialize(jsonWriter, value, T.TypeInfo);
        }

        static async ValueTask SerializeAsync(Stream stream, T value, CancellationToken token)
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
        else
        {
            var stream = IAsyncBinaryReader.GetStream(reader, out var keepAlive);
            result = keepAlive
                ? DeserializeFromStreamAsync(stream, token)
                : DeserializeFromStreamAndCloseAsync(stream, token);
        }

        return result;

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        static async ValueTask<JsonSerializable<T>> DeserializeFromStreamAsync(Stream readerStream, CancellationToken token)
        {
            return new()
            {
                Value = (await JsonSerializer.DeserializeAsync(readerStream, T.TypeInfo, token).ConfigureAwait(false))!,
            };
        }

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        static async ValueTask<JsonSerializable<T>> DeserializeFromStreamAndCloseAsync(Stream readerStream, CancellationToken token)
        {
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
}