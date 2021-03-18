using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    using Buffers;
    using IO;
    using IO.Log;

    /// <summary>
    /// Represents buffered log entry.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct BufferedRaftLogEntry : IRaftLogEntry, IDisposable
    {
        private const int BufferSize = 4096;

        // possible values are:
        // null - empty content
        // FileInfo - file name
        // IGrowableBuffer<byte> - in-memory copy of the log entry
        private readonly object? content;
        private readonly int? commandId;

        private BufferedRaftLogEntry(string fileName, long term, DateTimeOffset timestamp, int? id)
        {
            Term = term;
            Timestamp = timestamp;
            commandId = id;
            content = new FileInfo(fileName);
        }

        private BufferedRaftLogEntry(IGrowableBuffer<byte> buffer, long term, DateTimeOffset timestamp, int? id)
        {
            Term = term;
            Timestamp = timestamp;
            commandId = id;
            content = buffer;
        }

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
            FileInfo file => file.Length,
            IGrowableBuffer<byte> buffer => buffer.WrittenCount,
            _ => 0L
        };

        /// <inheritdoc/>
        long? IDataTransferObject.Length => Length;

        /// <inheritdoc/>
        bool IDataTransferObject.IsReusable => true;

        /// <inheritdoc/>
        bool ILogEntry.TryGetExtension<TExtension>(out TExtension extension)
        {
            if (content is FileInfo file)
            {
                extension = Unsafe.As<ContentLocationExtension, TExtension>(ref Unsafe.AsRef(new ContentLocationExtension(file)));
                return true;
            }

            extension = default;
            return false;
        }

        private static string GenerateFileName(string destinationPath)
            => Path.Combine(destinationPath, Guid.NewGuid().ToString());

        private static async ValueTask<BufferedRaftLogEntry> CopyToMemoryOrFileAsync<TEntry>(TEntry entry, string destinationPath, int memoryThreshold, MemoryAllocator<byte>? allocator, CancellationToken token)
            where TEntry : notnull, IRaftLogEntry
        {
            var writer = new FileBufferingWriter(new FileBufferingWriter.Options
            {
                MemoryAllocator = allocator,
                MemoryThreshold = memoryThreshold,
                AsyncIO = true,
                FileName = GenerateFileName(destinationPath),
            });

            try
            {
                await entry.WriteToAsync(writer, token).ConfigureAwait(false);
                await writer.FlushAsync(token).ConfigureAwait(false);
            }
            catch
            {
                await writer.DisposeAsync().ConfigureAwait(false);
                throw;
            }

            if (writer.TryGetWrittenContent(out _, out var fileName))
                return new BufferedRaftLogEntry(writer, entry.Term, entry.Timestamp, entry.CommandId);

            await writer.DisposeAsync().ConfigureAwait(false);
            return new BufferedRaftLogEntry(fileName, entry.Term, entry.Timestamp, entry.CommandId);
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

            return new BufferedRaftLogEntry(writer, entry.Term, entry.Timestamp, entry.CommandId);
        }

        private static async ValueTask<BufferedRaftLogEntry> CopyToFileAsync<TEntry>(TEntry entry, string destinationPath, long length, MemoryAllocator<byte>? allocator, CancellationToken token)
            where TEntry : notnull, IRaftLogEntry
        {
            using var buffer = allocator.Invoke(BufferSize, false);
            await using var output = new FileStream(GenerateFileName(destinationPath), FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous | FileOptions.WriteThrough);
            output.SetLength(length);
            await entry.WriteToAsync(output, buffer.Memory, token).ConfigureAwait(false);
            await output.FlushAsync(token).ConfigureAwait(false);
            return new BufferedRaftLogEntry(output.Name, entry.Term, entry.Timestamp, entry.CommandId);
        }

        internal static ValueTask<BufferedRaftLogEntry> CopyAsync<TEntry>(TEntry entry, string destinationPath, int memoryThreshold, MemoryAllocator<byte>? allocator, CancellationToken token)
            where TEntry : notnull, IRaftLogEntry
        {
            ValueTask<BufferedRaftLogEntry> result;
            if (!entry.Length.TryGetValue(out var length))
                result = CopyToMemoryOrFileAsync(entry, destinationPath, memoryThreshold, allocator, token);
            else if (length <= memoryThreshold)
                result = CopyToMemoryAsync(entry, (int)length, allocator, token);
            else
                result = CopyToFileAsync(entry, destinationPath, length, allocator, token);

            return result;
        }

        private static async ValueTask WriteFileAsync<TWriter>(string fileName, TWriter writer, CancellationToken token)
            where TWriter : notnull, IAsyncBinaryWriter
        {
            await using var input = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.SequentialScan | FileOptions.Asynchronous);
            await writer.CopyFromAsync(input, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token) => content switch
        {
            FileInfo file => WriteFileAsync(file.FullName, writer, token),
            IGrowableBuffer<byte> buffer => buffer.CopyToAsync(writer, token),
            _ => new ValueTask()
        };

        /// <summary>
        /// Releases all resources associated with the buffer.
        /// </summary>
        public void Dispose()
        {
            if (content is IDisposable disposable)
                disposable.Dispose();
        }
    }
}