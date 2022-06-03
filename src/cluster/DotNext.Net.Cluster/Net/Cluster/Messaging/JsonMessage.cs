using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace DotNext.Net.Cluster.Messaging;

using Buffers;
using IO;
using static Text.Json.JsonUtils;

/// <summary>
/// Represents JSON-serializable message.
/// </summary>
/// <typeparam name="T">JSON-serializable type.</typeparam>
public sealed class JsonMessage<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)]T> : IMessage
{
    private readonly object? optionsOrTypeInfo;

    /// <summary>
    /// Initializes a new message with JSON-serializable payload.
    /// </summary>
    /// <param name="name">The name of the message.</param>
    /// <param name="content">JSON-serializable object.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public JsonMessage(string name, T content)
    {
        Content = content;
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    /// <summary>
    /// Gets the name of this message.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the content of this message.
    /// </summary>
    public T Content { get; }

    /// <summary>
    /// Gets or sets JSON serialization options.
    /// </summary>
    public JsonSerializerOptions? Options
    {
        get => optionsOrTypeInfo as JsonSerializerOptions;
        init => optionsOrTypeInfo = value;
    }

    /// <summary>
    /// Gets or sets JSON type metadata.
    /// </summary>
    public JsonTypeInfo<T>? TypeInfo
    {
        get => optionsOrTypeInfo as JsonTypeInfo<T>;
        init => optionsOrTypeInfo = value;
    }

    /// <inheritdoc />
    ContentType IMessage.Type { get; } = new(MediaTypeNames.Application.Json);

    /// <inheritdoc />
    bool IDataTransferObject.IsReusable => true;

    /// <inheritdoc />
    long? IDataTransferObject.Length => null;

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "Public properties/fields are preserved")]
    private void SerializeToJson(IBufferWriter<byte> buffer)
    {
        using var jsonWriter = new Utf8JsonWriter(buffer, Options?.GetWriterOptions() ?? DefaultWriterOptions);

        var typeInfo = TypeInfo;
        if (typeInfo is null)
            JsonSerializer.Serialize(jsonWriter, Content, Options);
        else
            JsonSerializer.Serialize(jsonWriter, Content, typeInfo);
    }

    /// <inheritdoc />
    ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
    {
        var bufferWriter = writer.TryGetBufferWriter();
        ValueTask result;
        if (bufferWriter is null)
        {
            result = writer.WriteAsync(static (message, buffer) => message.SerializeToJson(buffer), this, token);
        }
        else
        {
            // fast path - serialize synchronously
            result = new();
            try
            {
                SerializeToJson(bufferWriter);
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }

        return result;
    }

    /// <summary>
    /// Deserializes object of type <typeparamref name="T"/> from the message.
    /// </summary>
    /// <param name="message">The message containing serialized object in JSON format.</param>
    /// <param name="options">Deserialization options.</param>
    /// <param name="allocator">The memory allocator for internal I/O manipulations.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>Deserialized object.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "Public properties/fields are preserved")]
    public static ValueTask<T?> FromJsonAsync(IDataTransferObject message, JsonSerializerOptions? options, MemoryAllocator<byte>? allocator = null, CancellationToken token = default)
    {
        ValueTask<T?> result;
        if (message.TryGetMemory(out var memory))
        {
            try
            {
                result = new(JsonSerializer.Deserialize<T>(memory.Span, options));
            }
            catch (Exception e)
            {
                result = ValueTask.FromException<T?>(e);
            }
        }
        else
        {
            result = DeserializeSlowAsync(message, options, allocator, token);
        }

        return result;

        static async ValueTask<T?> DeserializeSlowAsync(IDataTransferObject message, JsonSerializerOptions? options, MemoryAllocator<byte>? allocator, CancellationToken token)
        {
            using var utf8Bytes = await message.ToMemoryAsync(allocator, token).ConfigureAwait(false);
            return JsonSerializer.Deserialize<T>(utf8Bytes.Span, options);
        }
    }

    /// <summary>
    /// Deserializes object of type <typeparamref name="T"/> from the message.
    /// </summary>
    /// <param name="message">The message containing serialized object in JSON format.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>Deserialized object.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<T?> FromJsonAsync(IDataTransferObject message, CancellationToken token = default)
        => FromJsonAsync(message, options: null, allocator: null, token);

    /// <summary>
    /// Deserializes object of type <typeparamref name="T"/> from the message.
    /// </summary>
    /// <param name="message">The message containing serialized object in JSON format.</param>
    /// <param name="typeInfo">JSON type metadata.</param>
    /// <param name="allocator">The memory allocator for internal I/O manipulations.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>Deserialized object.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<T?> FromJsonAsync(IDataTransferObject message, JsonTypeInfo<T> typeInfo, MemoryAllocator<byte>? allocator = null, CancellationToken token = default)
    {
        ValueTask<T?> result;
        if (message.TryGetMemory(out var memory))
        {
            try
            {
                result = new(JsonSerializer.Deserialize<T>(memory.Span, typeInfo));
            }
            catch (Exception e)
            {
                result = ValueTask.FromException<T?>(e);
            }
        }
        else
        {
            result = DeserializeSlowAsync(message, typeInfo, allocator, token);
        }

        return result;

        static async ValueTask<T?> DeserializeSlowAsync(IDataTransferObject message, JsonTypeInfo<T> typeInfo, MemoryAllocator<byte>? allocator, CancellationToken token)
        {
            using var utf8Bytes = await message.ToMemoryAsync(allocator, token).ConfigureAwait(false);
            return JsonSerializer.Deserialize<T>(utf8Bytes.Span, typeInfo);
        }
    }
}