using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace DotNext.Net.Cluster.Consensus.Raft;

using IO;
using static Buffers.BufferWriter;

internal static class JsonLogEntry
{
    private const LengthFormat LengthEncoding = LengthFormat.PlainLittleEndian;
    private static readonly JsonReaderOptions DefaultReaderOptions = new JsonSerializerOptions(JsonSerializerDefaults.General).GetReaderOptions();

    private static Type LoadType(string typeId, Func<string, Type>? typeLoader)
        => typeLoader is null ? Type.GetType(typeId, true)! : typeLoader(typeId);

    private static Encoding DefaultEncoding => Encoding.UTF8;

    internal static ValueTask SerializeAsync<T, TWriter>(TWriter writer, string typeId, T obj, CancellationToken token)
        where TWriter : notnull, IAsyncBinaryWriter
    {
        // try to get synchronous writer
        var bufferWriter = writer.TryGetBufferWriter();
        ValueTask result;
        if (bufferWriter is null)
        {
            // slow path - delegate allocation is required and arguments must be packed
            result = writer.WriteAsync(SerializeToJson, (typeId, obj), token);
        }
        else
        {
            // fast path - synchronous serialization
            result = new();
            try
            {
                Serialize(typeId, obj, bufferWriter);
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }

        return result;

        static void Serialize(string typeId, T value, IBufferWriter<byte> buffer)
        {
            // serialize type identifier
            buffer.WriteString(typeId, DefaultEncoding, lengthFormat: LengthEncoding);

            // serialize object to JSON
            using var jsonWriter = new Utf8JsonWriter(buffer, new() { SkipValidation = false, Indented = false });
            JsonSerializer.Serialize(jsonWriter, value);
        }

        static void SerializeToJson((string TypeId, T Value) arg, IBufferWriter<byte> buffer)
            => Serialize(arg.TypeId, arg.Value, buffer);
    }

    private static JsonReaderOptions GetReaderOptions(this JsonSerializerOptions options) => new()
    {
        AllowTrailingCommas = options.AllowTrailingCommas,
        CommentHandling = options.ReadCommentHandling,
        MaxDepth = options.MaxDepth,
    };

    internal static object? Deserialize(SequenceReader input, Func<string, Type>? typeLoader, JsonSerializerOptions? options)
    {
        var typeId = input.ReadString(LengthEncoding, DefaultEncoding);
        var reader = new Utf8JsonReader(input.RemainingSequence, options?.GetReaderOptions() ?? DefaultReaderOptions);
        return JsonSerializer.Deserialize(ref reader, LoadType(typeId, typeLoader), options);
    }
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