using System.Buffers;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Net.Cluster.Consensus.Raft;

using IO;
using Buffers.Binary;
using Text.Json;

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
            init => content = metadata.Length > 0L ? value : IAsyncBinaryReader.Empty;
        }

        internal ReadOnlyMemory<byte> ContentBuffer
        {
            init
            {
                if (value.IsEmpty)
                {
                    content = IAsyncBinaryReader.Empty;
                }
                else if (MemoryMarshal.TryGetArray(value, out var segment))
                {
                    content = segment.Array;
                    contentOffset = segment.Offset;
                    contentLength = segment.Count;
                }
                else if (MemoryMarshal.TryGetMemoryManager(value, out MemoryManager<byte>? manager, out contentOffset, out contentLength))
                {
                    content = manager;
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

        // returns null if ROM<byte> is actual payload
        private IAsyncBinaryReader? GetReader(out ReadOnlyMemory<byte> buffer)
        {
            var tmp = content;
            switch (tmp)
            {
                case null:
                    tmp = IAsyncBinaryReader.Empty;
                    goto default;
                case byte[]:
                    buffer = Unsafe.As<byte[]>(tmp);
                    break;
                case FileReader:
                    Adjust(Unsafe.As<FileReader>(tmp), in metadata);
                    goto default;
                case MemoryManager<byte>:
                    buffer = Unsafe.As<MemoryManager<byte>>(tmp).Memory;
                    break;
                default:
                    Debug.Assert(tmp is IAsyncBinaryReader);

                    buffer = default;
                    return Unsafe.As<IAsyncBinaryReader>(tmp);
            }

            buffer = buffer.Slice(contentOffset, contentLength);
            return null;

            static void Adjust(FileReader reader, in LogEntryMetadata metadata)
            {
                if (!reader.HasBufferedData || metadata.Offset < reader.FilePosition || metadata.Offset > reader.ReadPosition)
                {
                    // attempt to read past or too far behind, clear the buffer
                    reader.Reset();
                    reader.FilePosition = metadata.Offset;
                }
                else
                {
                    // the offset is in the buffered segment within the file, skip necessary bytes
                    reader.Skip(metadata.Offset - reader.FilePosition);
                }

                reader.ReaderSegmentLength = metadata.Length;
            }
        }

        /// <inheritdoc/>
        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        {
            return GetReader(out var buffer) is { } reader
                ? reader.CopyToAsync(writer, count: null, token)
                : writer.WriteAsync(buffer, lengthFormat: null, token);
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
            return GetReader(out var buffer) is { } reader
                ? transformation.TransformAsync(reader, token)
                : transformation.TransformAsync(IAsyncBinaryReader.Create(buffer), token);
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
            if (GetReader(out memory) is not { } reader)
            {
                // nothing to do
            }
            else if (reader.TryGetSequence(out var sequence) && sequence.IsSingleSegment)
            {
                memory = sequence.First;
            }
            else
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets reader that can be used to deserialize the content of this log entry.
        /// </summary>
        /// <returns>The binary reader providing access to the content of this log entry.</returns>
        public IAsyncBinaryReader GetReader()
        {
            if (GetReader(out var buffer) is not { } reader)
                reader = IAsyncBinaryReader.Create(buffer);

            return reader;
        }
    }

    /// <summary>
    /// Creates a log entry with JSON-serializable payload.
    /// </summary>
    /// <typeparam name="T">JSON-serializable type.</typeparam>
    /// <param name="content">JSON-serializable content of the log entry.</param>
    /// <returns>The log entry encapsulating JSON-serializable content.</returns>
    /// <seealso cref="JsonSerializable{T}.TransformAsync{TInput}(TInput, CancellationToken)"/>
    public JsonLogEntry<T> CreateJsonLogEntry<T>(T? content)
        where T : notnull, IJsonSerializable<T>
        => new() { Term = Term, Content = content };

    /// <summary>
    /// Creates a log entry with binary payload.
    /// </summary>
    /// <param name="content">Binary payload.</param>
    /// <returns>The log entry encapsulating binary payload.</returns>
    public BinaryLogEntry CreateBinaryLogEntry(ReadOnlyMemory<byte> content)
        => new() { Term = Term, Content = content };

    /// <summary>
    /// Creates a log entry with binary payload.
    /// </summary>
    /// <typeparam name="T">The type representing a payload convertible to binary format.</typeparam>
    /// <param name="content">Binary payload.</param>
    /// <returns>The log entry encapsulating binary payload.</returns>
    public BinaryLogEntry<T> CreateBinaryLogEntry<T>(T content)
        where T : notnull, IBinaryFormattable<T>
        => new() { Term = Term, Content = content };
}