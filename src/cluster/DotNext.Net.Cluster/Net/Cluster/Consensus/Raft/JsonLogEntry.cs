#if !NETSTANDARD2_1
using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO;

    internal static class JsonLogEntry
    {
        private const LengthFormat LengthEncoding = LengthFormat.PlainLittleEndian;
        internal static readonly Func<string, Type> DefaultTypeLoader = LoadType;

        private static Type LoadType(string typeId) => Type.GetType(typeId, true)!;

        private static Encoding DefaultEncoding => Encoding.UTF8;

        internal static async ValueTask SerializeAsync<T, TWriter>(TWriter writer, string typeId, T obj, CancellationToken token)
            where TWriter : notnull, IAsyncBinaryWriter
        {
            // serialize type identifier
            await writer.WriteAsync(typeId.AsMemory(), DefaultEncoding, LengthEncoding, token).ConfigureAwait(false);
            await writer.WriteAsync(SerializeToJson, obj, token).ConfigureAwait(false);

            static void SerializeToJson(T obj, IBufferWriter<byte> buffer)
            {
                using var jsonWriter = new Utf8JsonWriter(buffer, new JsonWriterOptions { SkipValidation = false, Indented = false });
                JsonSerializer.Serialize(jsonWriter, obj);
            }
        }

        internal static async ValueTask<object?> DeserializeAsync(Stream input, Func<string, Type> typeLoader, JsonSerializerOptions? options, CancellationToken token)
            => await JsonSerializer.DeserializeAsync(input, typeLoader(await input.ReadStringAsync(LengthEncoding, DefaultEncoding, token).ConfigureAwait(false)), options, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Represents JSON-serializable log entry.
    /// </summary>
    /// <typeparam name="T">JSON-serializable type.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct JsonLogEntry<T> : IRaftLogEntry
    {
        private readonly JsonSerializerOptions? options;
        private readonly string? typeId;

        internal JsonLogEntry(long term, T content, string? typeId, JsonSerializerOptions? options)
        {
            Content = content;
            this.options = options;
            Term = term;
            Timestamp = DateTimeOffset.Now;
            this.typeId = typeId;
        }

        /// <summary>
        /// Gets the payload of this log entry.
        /// </summary>
        public T Content { get; }

        /// <summary>
        /// Gets Term value associated with this log entry.
        /// </summary>
        public long Term { get; }

        /// <summary>
        /// Gets the timestamp of this log entry.
        /// </summary>
        public DateTimeOffset Timestamp { get; }

        /// <inheritdoc />
        long? IDataTransferObject.Length => null;

        /// <inheritdoc />
        bool IDataTransferObject.IsReusable => true;

        private string TypeId
        {
            get
            {
                var result = typeId;
                if (string.IsNullOrEmpty(result))
                    result = typeof(T).AssemblyQualifiedName!;

                return result;
            }
        }

        /// <inheritdoc />
        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            => JsonLogEntry.SerializeAsync<T, TWriter>(writer, TypeId, Content, token);
    }
}
#endif