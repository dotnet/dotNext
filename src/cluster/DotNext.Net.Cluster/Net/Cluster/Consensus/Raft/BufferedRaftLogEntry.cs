using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Buffers;
    using IO;

    /// <summary>
    /// Represents buffered log entry.
    /// </summary>
    /// <remarks>
    /// This type is intended for developing transport-layer buffering
    /// and low level I/O optimizations when writing custom Write-Ahead Log.
    /// It's not recommended to use the type in the application code.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public readonly struct BufferedRaftLogEntry : IRaftLogEntry, IDisposable
    {
        // possible values are:
        // null - empty content
        // FileStream - file
        // IGrowableBuffer<byte> - in-memory copy of the log entry
        [SuppressMessage("Usage", "CA2213", Justification = "Disposed correctly by Dispose() method")]
        private readonly IDisposable? content;
        private readonly int? commandId;

        private BufferedRaftLogEntry(string fileName, int bufferSize, long term, DateTimeOffset timestamp, int? id, bool snapshot)
        {
            Term = term;
            Timestamp = timestamp;
            commandId = id;
            IsSnapshot = snapshot;
            content = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.None, bufferSize, FileOptions.SequentialScan | FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            InMemory = false;
        }

        private BufferedRaftLogEntry(FileStream file, long term, DateTimeOffset timestamp, int? id, bool snapshot)
        {
            Term = term;
            Timestamp = timestamp;
            commandId = id;
            content = file;
            IsSnapshot = snapshot;
            InMemory = false;
        }

        private BufferedRaftLogEntry(IGrowableBuffer<byte> buffer, long term, DateTimeOffset timestamp, int? id, bool snapshot)
        {
            Term = term;
            Timestamp = timestamp;
            commandId = id;
            content = buffer;
            IsSnapshot = snapshot;
            InMemory = true;
        }

        private BufferedRaftLogEntry(long term, DateTimeOffset timestamp, int? id, bool snapshot)
        {
            Term = term;
            Timestamp = timestamp;
            commandId = id;
            content = null;
            IsSnapshot = snapshot;
            InMemory = true;
        }

        internal bool InMemory { get; }

        /// <summary>
        /// Gets a value indicating whether the current log entry is a snapshot.
        /// </summary>
        public bool IsSnapshot { get; }

        /// <summary>
        /// Gets date/time of when log entry was created.
        /// </summary>
        public DateTimeOffset Timestamp { get; }

        /// <summary>
        /// Gets Term value associated with the log entry.
        /// </summary>
        public long Term { get; }

        /// <inheritdoc/>
        int? IRaftLogEntry.CommandId => commandId;

        /// <summary>
        /// Gets length of this log entry, in bytes.
        /// </summary>
        public long Length => content switch
        {
            FileStream file => file.Length,
            IGrowableBuffer<byte> buffer => buffer.WrittenCount,
            _ => 0L
        };

        /// <inheritdoc/>
        long? IDataTransferObject.Length => Length;

        /// <inheritdoc/>
        bool IDataTransferObject.IsReusable => true;

        private static async ValueTask<BufferedRaftLogEntry> CopyToMemoryOrFileAsync<TEntry>(TEntry entry, RaftLogEntryBufferingOptions options, CancellationToken token)
            where TEntry : notnull, IRaftLogEntry
        {
            var writer = options.CreateBufferingWriter();
            var buffer = options.RentBuffer();
            try
            {
                await entry.WriteToAsync(writer, buffer.Memory, token).ConfigureAwait(false);
                await writer.FlushAsync(token).ConfigureAwait(false);
            }
            catch
            {
                await writer.DisposeAsync().ConfigureAwait(false);
                throw;
            }
            finally
            {
                buffer.Dispose();
                buffer = default;
            }

            if (writer.TryGetWrittenContent(out _, out var fileName))
                return new BufferedRaftLogEntry(writer, entry.Term, entry.Timestamp, entry.CommandId, entry.IsSnapshot);

            await writer.DisposeAsync().ConfigureAwait(false);
            return new BufferedRaftLogEntry(fileName, options.BufferSize, entry.Term, entry.Timestamp, entry.CommandId, entry.IsSnapshot);
        }

        private static async ValueTask<BufferedRaftLogEntry> CopyToMemoryAsync<TEntry>(TEntry entry, int length, MemoryAllocator<byte>? allocator, CancellationToken token)
            where TEntry : notnull, IRaftLogEntry
        {
            var writer = new PooledBufferWriter<byte>(allocator, length);
            try
            {
                await entry.WriteToAsync(writer, token).ConfigureAwait(false);
            }
            catch
            {
                writer.Dispose();
                throw;
            }

            return new BufferedRaftLogEntry(writer, entry.Term, entry.Timestamp, entry.CommandId, entry.IsSnapshot);
        }

        internal static async ValueTask<BufferedRaftLogEntry> CopyToFileAsync<TEntry>(TEntry entry, RaftLogEntryBufferingOptions options, long? length, CancellationToken token)
            where TEntry : notnull, IRaftLogEntry
        {
            var output = new FileStream(options.GetRandomFileName(), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, options.BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);
            var buffer = options.RentBuffer();
            try
            {
                if (length.HasValue)
                    output.SetLength(length.GetValueOrDefault());

                await entry.WriteToAsync(output, buffer.Memory, token).ConfigureAwait(false);
                await output.FlushAsync(token).ConfigureAwait(false);
            }
            catch
            {
                await output.DisposeAsync().ConfigureAwait(false);
                throw;
            }
            finally
            {
                buffer.Dispose();
                buffer = default;
            }

            return new BufferedRaftLogEntry(output, entry.Term, entry.Timestamp, entry.CommandId, entry.IsSnapshot);
        }

        /// <summary>
        /// Constructs a copy of the specified log entry.
        /// </summary>
        /// <param name="entry">The log entry to be copied.</param>
        /// <param name="options">Buffering options.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <typeparam name="TEntry">The type of the log entry to be copied.</typeparam>
        /// <returns>Buffered copy of <paramref name="entry"/>.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<BufferedRaftLogEntry> CopyAsync<TEntry>(TEntry entry, RaftLogEntryBufferingOptions options, CancellationToken token)
            where TEntry : notnull, IRaftLogEntry
        {
            ValueTask<BufferedRaftLogEntry> result;
            if (!entry.Length.TryGetValue(out var length))
                result = CopyToMemoryOrFileAsync(entry, options, token);
            else if (length == 0L)
                result = new(new BufferedRaftLogEntry(entry.Term, entry.Timestamp, entry.CommandId, entry.IsSnapshot));
            else if (length <= options.MemoryThreshold)
                result = CopyToMemoryAsync(entry, (int)length, options.MemoryAllocator, token);
            else
                result = CopyToFileAsync(entry, options, length, token);

            return result;
        }

        /// <inheritdoc />
        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        {
            ValueTask result;
            switch (content)
            {
                case FileStream fs:
                    fs.Position = 0L;
                    result = new(writer.CopyFromAsync(fs, token));
                    break;
                case IGrowableBuffer<byte> buffer:
                    result = buffer.CopyToAsync(writer, token);
                    break;
                default:
                    result = new();
                    break;
            }

            return result;
        }

        /// <summary>
        /// Releases all resources associated with the buffer.
        /// </summary>
        public void Dispose() => content?.Dispose();
    }
}