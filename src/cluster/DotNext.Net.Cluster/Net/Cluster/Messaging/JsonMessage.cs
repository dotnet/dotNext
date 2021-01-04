#if !NETSTANDARD2_1
using System;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    using Buffers;
    using IO;

    /// <summary>
    /// Represents JSON-serializable message.
    /// </summary>
    /// <typeparam name="T">JSON-serializable type.</typeparam>
    public sealed class JsonMessage<T> : IMessage
    {
        private JsonSerializerOptions? options;

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
            get => options;
            set => options = value;
        }

        private JsonWriterOptions WriterOptions
        {
            get
            {
                var result = new JsonWriterOptions { SkipValidation = false };
                if (options is not null)
                {
                    result.Encoder = options.Encoder;
                    result.Indented = options.WriteIndented;
                }

                return result;
            }
        }

        /// <inheritdoc />
        ContentType IMessage.Type { get; } = new ContentType(MediaTypeNames.Application.Json);

        /// <inheritdoc />
        bool IDataTransferObject.IsReusable => true;

        /// <inheritdoc />
        long? IDataTransferObject.Length => null;

        /// <inheritdoc />
        async ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        {
            using var buffer = new PooledArrayBufferWriter<byte>();
            using var jsonWriter = new Utf8JsonWriter(buffer, WriterOptions);
            JsonSerializer.Serialize(jsonWriter, Content, options);
            await writer.WriteAsync(buffer.WrittenMemory, null, token).ConfigureAwait(false);
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
        public static async ValueTask<T?> FromJsonAsync(IMessage message, JsonSerializerOptions? options = null, MemoryAllocator<byte>? allocator = null, CancellationToken token = default)
        {
            using var utf8Bytes = await message.ToMemoryAsync(allocator, token).ConfigureAwait(false);
            return JsonSerializer.Deserialize<T>(utf8Bytes.Memory.Span, options);
        }

        /// <summary>
        /// Deserializes object of type <typeparamref name="T"/> from the message.
        /// </summary>
        /// <param name="message">The message containing serialized object in JSON format.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>Deserialized object.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<T?> FromJsonAsync(IMessage message, CancellationToken token = default)
            => FromJsonAsync(message, null, null, token);
    }
}
#endif