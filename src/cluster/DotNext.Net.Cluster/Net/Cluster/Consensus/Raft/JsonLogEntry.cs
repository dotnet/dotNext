using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft;

using IO;
using static Buffers.BufferWriter;
using static Text.Json.JsonUtils;

internal static class JsonLogEntry
{
    private const LengthFormat LengthEncoding = LengthFormat.PlainLittleEndian;

    [RequiresUnreferencedCode("JSON deserialization may be incompatible with IL trimming")]
    private static Type LoadType(string typeId, Func<string, Type>? typeLoader)
        => typeLoader is null ? Type.GetType(typeId, true)! : typeLoader(typeId);

    private static Encoding DefaultEncoding => Encoding.UTF8;

    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026", Justification = "Public properties/fields are preserved")]
    internal static ValueTask SerializeAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)]T, TWriter>(TWriter writer, string typeId, T obj, JsonSerializerOptions? options, CancellationToken token)
        where TWriter : notnull, IAsyncBinaryWriter
    {
        // try to get synchronous writer
        var bufferWriter = writer.TryGetBufferWriter();
        ValueTask result;
        if (bufferWriter is null)
        {
            // slow path - delegate allocation is required and arguments must be packed
            result = writer.WriteAsync(SerializeToJson, (typeId, obj, options), token);
        }
        else
        {
            // fast path - synchronous serialization
            result = new();
            try
            {
                Serialize(typeId, obj, bufferWriter, options);
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }

        return result;

        static void Serialize(string typeId, T value, IBufferWriter<byte> buffer, JsonSerializerOptions? options)
        {
            // serialize type identifier
            buffer.WriteString(typeId, DefaultEncoding, lengthFormat: LengthEncoding);

            // serialize object to JSON
            using var jsonWriter = new Utf8JsonWriter(buffer, options?.GetWriterOptions() ?? DefaultWriterOptions);
            JsonSerializer.Serialize(jsonWriter, value, options);
        }

        static void SerializeToJson((string TypeId, T Value, JsonSerializerOptions? Options) arg, IBufferWriter<byte> buffer)
            => Serialize(arg.TypeId, arg.Value, buffer, arg.Options);
    }

    internal static ValueTask SerializeAsync<T, TWriter>(TWriter writer, string typeId, T obj, JsonTypeInfo<T> typeInfo, CancellationToken token)
        where TWriter : notnull, IAsyncBinaryWriter
    {
        // try to get synchronous writer
        var bufferWriter = writer.TryGetBufferWriter();
        ValueTask result;
        if (bufferWriter is null)
        {
            // slow path - delegate allocation is required and arguments must be packed
            result = writer.WriteAsync(SerializeToJson, (typeId, obj, typeInfo), token);
        }
        else
        {
            // fast path - synchronous serialization
            result = new();
            try
            {
                Serialize(typeId, obj, bufferWriter, typeInfo);
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }

        return result;

        static void Serialize(string typeId, T value, IBufferWriter<byte> buffer, JsonTypeInfo<T> typeInfo)
        {
            // serialize type identifier
            buffer.WriteString(typeId, DefaultEncoding, lengthFormat: LengthEncoding);

            // serialize object to JSON
            using var jsonWriter = new Utf8JsonWriter(buffer, DefaultWriterOptions);
            JsonSerializer.Serialize(jsonWriter, value, typeInfo);
        }

        static void SerializeToJson((string TypeId, T Value, JsonTypeInfo<T> TypeInfo) arg, IBufferWriter<byte> buffer)
            => Serialize(arg.TypeId, arg.Value, buffer, arg.TypeInfo);
    }

    [RequiresUnreferencedCode("JSON deserialization may be incompatible with IL trimming")]
    internal static object? Deserialize(SequenceReader input, Func<string, Type>? typeLoader, JsonSerializerOptions? options)
    {
        var typeId = input.ReadString(LengthEncoding, DefaultEncoding);
        var reader = new Utf8JsonReader(input.RemainingSequence, options?.GetReaderOptions() ?? DefaultReaderOptions);
        return JsonSerializer.Deserialize(ref reader, LoadType(typeId, typeLoader), options);
    }

    internal static object? Deserialize(SequenceReader input, Func<string, Type> typeLoader, JsonSerializerContext context)
    {
        Debug.Assert(typeLoader is not null);
        Debug.Assert(context is not null);

        var typeId = input.ReadString(LengthEncoding, DefaultEncoding);
        var reader = new Utf8JsonReader(input.RemainingSequence, context.Options.GetReaderOptions());
        return JsonSerializer.Deserialize(ref reader, typeLoader(typeId), context);
    }
}

/// <summary>
/// Represents JSON-serializable log entry.
/// </summary>
/// <typeparam name="T">JSON-serializable type.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly struct JsonLogEntry<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)]T> : IRaftLogEntry
{
    private readonly object? optionsOrTypeInfo;
    private readonly string? typeId;

    internal JsonLogEntry(long term, T content, string? typeId, JsonSerializerOptions? options)
    {
        Content = content;
        optionsOrTypeInfo = options;
        Term = term;
        Timestamp = DateTimeOffset.Now;
        this.typeId = typeId;
    }

    internal JsonLogEntry(long term, T content, string? typeId, JsonTypeInfo<T> typeInfo)
    {
        Debug.Assert(typeInfo is not null);

        Content = content;
        optionsOrTypeInfo = typeInfo;
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

    /// <inheritdoc />
    int? IRaftLogEntry.CommandId => null;

    private string TypeId => typeId is { Length: > 0 } result ? result : typeof(T).AssemblyQualifiedName!;

    /// <inheritdoc />
    ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        => optionsOrTypeInfo is JsonTypeInfo<T> typeInfo
            ? JsonLogEntry.SerializeAsync(writer, TypeId, Content, typeInfo, token)
            : JsonLogEntry.SerializeAsync(writer, TypeId, Content, optionsOrTypeInfo as JsonSerializerOptions, token);
}