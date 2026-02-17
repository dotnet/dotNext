using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace DotNext.Text.Json;

using IO;
using IO.Pipelines;
using Runtime.Serialization;

/// <summary>
/// Represents a bridge between JSON serialization framework in .NET and <see cref="ISerializable{TSelf}"/>
/// interface.
/// </summary>
/// <typeparam name="T">JSON serializable type.</typeparam>
[StructLayout(LayoutKind.Auto)]
public record struct JsonSerializable<T> : ISerializable<JsonSerializable<T>>, ISupplier<T?>
    where T : IJsonSerializable<T>
{
    /// <summary>
    /// Represents JSON serializable object.
    /// </summary>
    required public T? Value;

    /// <inheritdoc />
    readonly T? ISupplier<T?>.Invoke() => Value;

    /// <inheritdoc />
    readonly long? IDataTransferObject.Length => null;

    /// <summary>
    /// Writes one JSON value (including objects or arrays) to the provided writer.
    /// </summary>
    /// <typeparam name="TWriter">The type of the writer.</typeparam>
    /// <param name="writer">UTF-8 output to write to.</param>
    /// <param name="value">The value to convert.</param>
    /// <param name="token">The token that can be used to cancel the write operation.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    public static ValueTask SerializeAsync<TWriter>(TWriter writer, T? value, CancellationToken token)
        where TWriter : IAsyncBinaryWriter
    {
        ValueTask result;

        if (typeof(TWriter) == typeof(PipeBinaryWriter))
        {
            result = new(JsonSerializer.SerializeAsync(Unsafe.As<TWriter, PipeBinaryWriter>(ref writer).Writer,
                value,
                T.TypeInfo!,
                token));
        }
        else if (typeof(TWriter) == typeof(AsyncStreamBinaryAccessor))
        {
            result = new(JsonSerializer.SerializeAsync(Unsafe.As<TWriter, AsyncStreamBinaryAccessor>(ref writer).Stream,
                value,
                T.TypeInfo!,
                token));
        }
        else if (writer.TryGetBufferWriter() is { } bufferWriter)
        {
            // fast path - synchronous serialization
            result = ValueTask.CompletedTask;
            try
            {
                Serialize(value, bufferWriter);
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }
        else
        {
            result = SerializeAsync(IAsyncBinaryWriter.CreateStream(writer), value, token);
        }

        return result;

        static void Serialize(T? value, IBufferWriter<byte> writer)
        {
            using var jsonWriter = new Utf8JsonWriter(writer, new JsonWriterOptions { Indented = false, SkipValidation = false });
            JsonSerializer.Serialize(jsonWriter, value, T.TypeInfo!);
        }

        static async ValueTask SerializeAsync(Stream stream, T? value, CancellationToken token)
        {
            try
            {
                await JsonSerializer.SerializeAsync(stream, value, T.TypeInfo!, token).ConfigureAwait(false);
            }
            finally
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    readonly ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        => SerializeAsync(writer, Value, token);

    /// <summary>
    /// Reads the UTF-8 encoded text representing a single JSON value.
    /// The input will be read to completion.
    /// </summary>
    /// <typeparam name="TReader">The type of the reader.</typeparam>
    /// <param name="reader">The input containing UTF-8 encoded text.</param>
    /// <param name="token">The token which may be used to cancel the read operation.</param>
    /// <returns>A value deserialized from JSON.</returns>
    public static ValueTask<T?> DeserializeAsync<TReader>(TReader reader, CancellationToken token = default)
        where TReader : IAsyncBinaryReader
    {
        ValueTask<T?> result;

        if (typeof(TReader) == typeof(PipeBinaryReader))
        {
            result = JsonSerializer.DeserializeAsync(Unsafe.As<TReader, PipeBinaryReader>(ref reader).Reader,
                T.TypeInfo,
                token);
        }
        else if (typeof(TReader) == typeof(AsyncStreamBinaryAccessor))
        {
            result = JsonSerializer.DeserializeAsync(Unsafe.As<TReader, AsyncStreamBinaryAccessor>(ref reader).Stream,
                T.TypeInfo,
                token);
        }
        else if (reader.TryGetSequence(out var buffer))
        {
            // fast path - synchronous deserialization
            try
            {
                result = new(Deserialize(buffer));
            }
            catch (Exception e)
            {
                result = ValueTask.FromException<T?>(e);
            }
        }
        else
        {
            result = DeserializeFromStreamAndCloseAsync(IAsyncBinaryReader.CreateStream(reader), token);
        }

        return result;

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        static async ValueTask<T?> DeserializeFromStreamAndCloseAsync(Stream readerStream, CancellationToken token)
        {
            try
            {
                return await JsonSerializer.DeserializeAsync(readerStream, T.TypeInfo, token).ConfigureAwait(false);
            }
            finally
            {
                await readerStream.DisposeAsync().ConfigureAwait(false);
            }
        }

        static T? Deserialize(ReadOnlySequence<byte> buffer)
        {
            var jsonReader = new Utf8JsonReader(buffer);
            return JsonSerializer.Deserialize(ref jsonReader, T.TypeInfo);
        }
    }

    /// <summary>
    /// Converts an object to JSON-serializable data transfer object.
    /// </summary>
    /// <typeparam name="TInput">The type of the object to transform.</typeparam>
    /// <param name="input">The object to transform.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>Deserialized object.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<T?> TransformAsync<TInput>(TInput input, CancellationToken token = default)
        where TInput : IDataTransferObject
        => input.TransformAsync<T?, DeserializingTransformation>(new(), token);

    /// <inheritdoc cref="ISerializable{TSelf}.ReadFromAsync{TReader}(TReader, CancellationToken)"/>
    public static async ValueTask<JsonSerializable<T>> ReadFromAsync<TReader>(TReader reader, CancellationToken token = default)
        where TReader : IAsyncBinaryReader
        => new() { Value = await DeserializeAsync(reader, token).ConfigureAwait(false) };

    /// <inheritdoc />
    public readonly override string? ToString() => Value?.ToString();

    [StructLayout(LayoutKind.Auto)]
    private readonly struct DeserializingTransformation : IDataTransferObject.ITransformation<T?>
    {
        ValueTask<T?> IDataTransferObject.ITransformation<T?>.TransformAsync<TReader>(TReader reader, CancellationToken token)
            => DeserializeAsync(reader, token);
    }
}