using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using IO;

public partial class PersistentState
{
    /// <summary>
    /// Represents persistent log entry.
    /// </summary>
    /// <remarks>
    /// Use <see cref="TransformAsync"/> to decode the log entry.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    protected internal readonly struct LogEntry : IRaftLogEntry
    {
        // null (if empty), FileReader, IAsyncBinaryReader, or byte[], or MemoryManager<byte>
        private readonly object? content;
        private readonly int contentOffset, contentLength;
        private readonly LogEntryMetadata metadata;

        // if negative then it's a snapshot index because |snapshotIndex| > 0
        private readonly long index;

        // for regular log entry
        internal LogEntry(in LogEntryMetadata metadata, long index)
        {
            this.metadata = metadata;
            this.index = index;
        }

        // for snapshot
        internal LogEntry(in SnapshotMetadata metadata)
        {
            Debug.Assert(metadata.Index > 0L);

            this.metadata = metadata.RecordMetadata;
            index = -metadata.Index;
        }

        internal IAsyncBinaryReader? ContentReader
        {
            init => content = metadata.Length > 0L ? value : null;
        }

        internal Memory<byte> ContentBuffer
        {
            init
            {
                if (MemoryMarshal.TryGetArray<byte>(value, out var segment))
                {
                    content = segment.Array;
                    contentOffset = segment.Offset;
                    contentLength = segment.Count;
                }
                else if (MemoryMarshal.TryGetMemoryManager<byte, MemoryManager<byte>>(value, out var manager, out contentOffset, out contentLength))
                {
                    content = manager;
                }
                else
                {
                    content = null;
                }
            }
        }

        internal static LogEntry Initial => new();

        internal long? SnapshotIndex
        {
            get
            {
                var i = -index;
                return i > 0L ? i : null;
            }
        }

        internal long Position => metadata.Offset;

        /// <summary>
        /// Gets the index of this log entry.
        /// </summary>
        public long Index => Math.Abs(index);

        /// <summary>
        /// Gets identifier of the command encapsulated by this log entry.
        /// </summary>
        public int? CommandId => metadata.Id;

        /// <summary>
        /// Gets a value indicating that this entry is a snapshot entry.
        /// </summary>
        public bool IsSnapshot => index < 0L;

        /// <summary>
        /// Gets length of the log entry content, in bytes.
        /// </summary>
        public long Length => metadata.Length;

        internal bool IsEmpty => Length == 0L;

        private bool TryGetContentAsReader([NotNullWhen(true)] out IAsyncBinaryReader? result)
        {
            switch (content)
            {
                case FileReader reader:
                    Adjust(reader, in metadata);
                    result = reader;
                    break;
                case IAsyncBinaryReader reader:
                    result = reader;
                    break;
                default:
                    result = null;
                    return false;
            }

            return true;

            static void Adjust(FileReader reader, in LogEntryMetadata metadata)
            {
                if (!reader.HasBufferedData || metadata.Offset < reader.FilePosition || metadata.Offset > reader.ReadPosition)
                {
                    // attempt to read past or too far behind, clear the buffer
                    reader.ClearBuffer();
                    reader.FilePosition = metadata.Offset;
                }
                else
                {
                    // the offset is in the buffered segment within the file, skip necessary bytes
                    reader.Skip(metadata.Offset - reader.FilePosition);
                }

                reader.SetSegmentLength(metadata.Length);
            }
        }

        private bool TryGetContentAsMemory(out ReadOnlyMemory<byte> buffer)
        {
            switch (content)
            {
                case byte[] array:
                    buffer = array;
                    break;
                case MemoryManager<byte> manager:
                    buffer = manager.Memory;
                    break;
                default:
                    buffer = default;
                    return false;
            }

            buffer = buffer.Slice(contentOffset, contentLength);
            return true;
        }

        /// <inheritdoc/>
        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        {
            ValueTask result;
            if (TryGetContentAsReader(out var reader))
            {
                result = new(reader.CopyToAsync(writer, token));
            }
            else if (TryGetContentAsMemory(out var buffer))
            {
                result = writer.WriteAsync(buffer, lengthFormat: null, token);
            }
            else
            {
                result = ValueTask.CompletedTask;
            }

            return result;
        }

        /// <inheritdoc/>
        long? IDataTransferObject.Length => Length;

        /// <inheritdoc/>
        bool IDataTransferObject.IsReusable => content is not FileReader;

        /// <summary>
        /// Gets Raft term of this log entry.
        /// </summary>
        public long Term => metadata.Term;

        /// <summary>
        /// Gets timestamp of this log entry.
        /// </summary>
        public DateTimeOffset Timestamp => new(metadata.Timestamp, TimeSpan.Zero);

        /// <inheritdoc/>
        public ValueTask<TResult> TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
            where TTransformation : notnull, IDataTransferObject.ITransformation<TResult>
        {
            ValueTask<TResult> result;
            if (TryGetContentAsReader(out var reader))
            {
                result = transformation.TransformAsync(reader, token);
            }
            else if (TryGetContentAsMemory(out var buffer))
            {
                result = transformation.TransformAsync(IAsyncBinaryReader.Create(buffer.Slice(contentOffset, contentLength)), token);
            }
            else
            {
                result = IDataTransferObject.Empty.TransformAsync<TResult, TTransformation>(transformation, token);
            }

            return result;
        }

        /// <summary>
        /// Attempts to obtain the payload of this log entry in the form of the memory block.
        /// </summary>
        /// <remarks>
        /// This method returns <see langword="false"/> if the log entry is not cached
        /// in the memory. Use <see cref="TransformAsync{TResult, TTransformation}(TTransformation, CancellationToken)"/>
        /// as a uniform way to deserialize this payload.
        /// </remarks>
        /// <param name="memory">The memory block representing the log entry payload.</param>
        /// <returns><see langword="true"/> if the log entry payload is available as a memory block; otherwise, <see langword="false"/>.</returns>
        public bool TryGetMemory(out ReadOnlyMemory<byte> memory)
        {
            if (content is null)
            {
                memory = ReadOnlyMemory<byte>.Empty;
                return true;
            }

            return TryGetContentAsMemory(out memory);
        }

        /// <summary>
        /// Gets reader that can be used to deserialize the content of this log entry.
        /// </summary>
        /// <returns>The binary reader providing access to the content of this log entry.</returns>
        public IAsyncBinaryReader GetReader()
        {
            if (TryGetContentAsReader(out var reader))
                return reader;

            if (TryGetContentAsMemory(out var buffer))
                return IAsyncBinaryReader.Create(buffer);

            return IAsyncBinaryReader.Empty;
        }

        /// <summary>
        /// Deserializes JSON content represented by this log entry.
        /// </summary>
        /// <param name="typeLoader">
        /// The type loader responsible for resolving the type to be deserialized.
        /// If <see langword="null"/> then <see cref="Type.GetType(string, bool)"/> is used
        /// for type resolution.
        /// </param>
        /// <param name="options">Deserialization options.</param>
        /// <param name="token">The token that can be used to cancel the deserialization.</param>
        /// <returns>The deserialized object.</returns>
        /// <exception cref="TypeLoadException"><paramref name="typeLoader"/> unable to resolve the type.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <seealso cref="CreateJsonLogEntry{T}(T, string?, JsonSerializerOptions?)"/>
        [RequiresUnreferencedCode("JSON deserialization may be incompatible with IL trimming")]
        public ValueTask<object?> DeserializeFromJsonAsync(Func<string, Type>? typeLoader = null, JsonSerializerOptions? options = null, CancellationToken token = default)
        {
            ValueTask<object?> result;

            if (TryGetContentAsReader(out var reader))
            {
                result = DeserializeSlowAsync(reader, metadata, typeLoader, options, token);
            }
            else if (TryGetContentAsMemory(out var buffer))
            {
                try
                {
                    result = new(JsonLogEntry.Deserialize(IAsyncBinaryReader.Create(buffer.Slice(contentOffset, contentLength)), typeLoader, options));
                }
                catch (Exception e)
                {
                    result = ValueTask.FromException<object?>(e);
                }
            }
            else
            {
                result = new(result: null);
            }

            return result;
        }

        [RequiresUnreferencedCode("JSON deserialization may be incompatible with IL trimming")]
        private static async ValueTask<object?> DeserializeSlowAsync(IAsyncBinaryReader reader, LogEntryMetadata metadata, Func<string, Type>? typeLoader, JsonSerializerOptions? options, CancellationToken token)
        {
            using var buffer = MemoryAllocator.Allocate<byte>(metadata.Length.Truncate(), true);
            await reader.ReadAsync(buffer.Memory, token).ConfigureAwait(false);
            return JsonLogEntry.Deserialize(IAsyncBinaryReader.Create(buffer.Memory), typeLoader, options);
        }

        /// <summary>
        /// Deserializes JSON content represented by this log entry.
        /// </summary>
        /// <param name="typeLoader">
        /// This overload is compatible with IL trimming and doesn't involve Reflection.
        /// </param>
        /// <param name="context">Deserialization context.</param>
        /// <param name="token">The token that can be used to cancel the deserialization.</param>
        /// <returns>The deserialized object.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="typeLoader"/> or <paramref name="context"/> is <see langword="null"/>.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <seealso cref="CreateJsonLogEntry{T}(T, string?, JsonTypeInfo{T})"/>
        public ValueTask<object?> DeserializeFromJsonAsync(Func<string, Type> typeLoader, JsonSerializerContext context, CancellationToken token = default)
        {
            ValueTask<object?> result;

            if (typeLoader is null)
            {
                result = ValueTask.FromException<object?>(new ArgumentNullException(nameof(typeLoader)));
            }
            else if (context is null)
            {
                result = ValueTask.FromException<object?>(new ArgumentNullException(nameof(context)));
            }
            else if (TryGetContentAsReader(out var reader))
            {
                result = DeserializeSlowAsync(reader, metadata, typeLoader, context, token);
            }
            else if (TryGetContentAsMemory(out var buffer))
            {
                try
                {
                    result = new(JsonLogEntry.Deserialize(IAsyncBinaryReader.Create(buffer.Slice(contentOffset, contentLength)), typeLoader, context));
                }
                catch (Exception e)
                {
                    result = ValueTask.FromException<object?>(e);
                }
            }
            else
            {
                result = new(result: null);
            }

            return result;
        }

        private static async ValueTask<object?> DeserializeSlowAsync(IAsyncBinaryReader reader, LogEntryMetadata metadata, Func<string, Type> typeLoader, JsonSerializerContext context, CancellationToken token)
        {
            using var buffer = MemoryAllocator.Allocate<byte>(metadata.Length.Truncate(), true);
            await reader.ReadAsync(buffer.Memory, token).ConfigureAwait(false);
            return JsonLogEntry.Deserialize(IAsyncBinaryReader.Create(buffer.Memory), typeLoader, context);
        }
    }

    /// <summary>
    /// Creates a log entry with JSON-serializable payload.
    /// </summary>
    /// <typeparam name="T">JSON-serializable type.</typeparam>
    /// <param name="content">JSON-serializable content of the log entry.</param>
    /// <param name="typeId">
    /// The type identifier required to recognize the correct type during deserialization.
    /// If <see langword="null"/> then <see cref="Type.AssemblyQualifiedName"/> of <typeparamref name="T"/> is used as type identifier.
    /// </param>
    /// <param name="options">Serialization options.</param>
    /// <returns>The log entry representing JSON-serializable content.</returns>
    /// <seealso cref="LogEntry.DeserializeFromJsonAsync(Func{string, Type}?, JsonSerializerOptions?, CancellationToken)"/>
    public JsonLogEntry<T> CreateJsonLogEntry<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicConstructors)]T>(T content, string? typeId = null, JsonSerializerOptions? options = null)
        => new(Term, content, typeId, options);

    /// <summary>
    /// Creates a log entry with JSON-serializable payload.
    /// </summary>
    /// <remarks>
    /// This overload is compatible with IL trimming and doesn't involve Reflection.
    /// </remarks>
    /// <typeparam name="T">JSON-serializable type.</typeparam>
    /// <param name="content">JSON-serializable content of the log entry.</param>
    /// <param name="typeId">The type identifier required to recognize the correct type during deserialization.</param>
    /// <param name="typeInfo">The metadata of the type required for serialization.</param>
    /// <returns>The log entry representing JSON-serializable content.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="typeId"/> or <paramref name="typeInfo"/> is <see langword="null"/>.</exception>
    /// <seealso cref="LogEntry.DeserializeFromJsonAsync(Func{string, Type}, JsonSerializerContext, CancellationToken)"/>
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2091", Justification = "Type information is supplied using JsonTypeInfo without the need of Reflection")]
    public JsonLogEntry<T> CreateJsonLogEntry<T>(T content, string typeId, JsonTypeInfo<T> typeInfo)
        => new(Term, content, typeId ?? throw new ArgumentNullException(nameof(typeId)), typeInfo ?? throw new ArgumentNullException(nameof(typeInfo)));
}