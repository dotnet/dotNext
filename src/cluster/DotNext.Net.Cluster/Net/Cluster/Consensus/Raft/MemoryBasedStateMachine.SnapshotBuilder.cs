using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SafeFileHandle = Microsoft.Win32.SafeHandles.SafeFileHandle;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using IO;
using IO.Log;

public partial class MemoryBasedStateMachine
{
    /*
     * Binary format:
     * [struct SnapshotMetadata] X 1
     * [octet string] X 1
     */
    internal sealed class Snapshot : ConcurrentStorageAccess, ISnapshotReader
    {
        private new const string FileName = "snapshot";
        private const string TempFileName = "snapshot.new";

        private MemoryOwner<byte> metadataBuffer;
        private SnapshotMetadata metadata;

        internal Snapshot(DirectoryInfo location, int bufferSize, in BufferManager manager, int readersCount, bool writeThrough, bool tempSnapshot = false, long initialSize = 0L)
            : base(Path.Combine(location.FullName, tempSnapshot ? TempFileName : FileName), SnapshotMetadata.Size, bufferSize, manager.BufferAllocator, readersCount, GetOptions(writeThrough), initialSize)
        {
            metadataBuffer = manager.BufferAllocator.Invoke(SnapshotMetadata.Size, true);
        }

        // cache flag that allows to avoid expensive access to Length that can cause native call
        internal bool IsEmpty => metadata.Index == 0L;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static FileOptions GetOptions(bool writeThrough)
        {
            const FileOptions skipBufferOptions = FileOptions.Asynchronous | FileOptions.WriteThrough;
            const FileOptions dontSkipBufferOptions = FileOptions.Asynchronous;
            return writeThrough ? skipBufferOptions : dontSkipBufferOptions;
        }

        internal void Initialize()
        {
            using var task = InitializeAsync();
            task.Wait();
        }

        private async Task InitializeCoreAsync()
        {
            if (await RandomAccess.ReadAsync(Handle, metadataBuffer.Memory, 0L).ConfigureAwait(false) < fileOffset)
            {
                metadataBuffer.Span.Clear();
                await RandomAccess.WriteAsync(Handle, metadataBuffer.Memory, 0L).ConfigureAwait(false);
            }

            metadata = new(metadataBuffer.Span);
        }

        internal Task InitializeAsync()
            => FileSize >= fileOffset ? InitializeCoreAsync() : Task.CompletedTask;

        private ReadOnlyMemory<byte> SerializeMetadata()
        {
            metadata.Format(metadataBuffer.Span);
            return metadataBuffer.Memory;
        }

        public override async ValueTask FlushAsync(CancellationToken token = default)
        {
            await RandomAccess.WriteAsync(Handle, SerializeMetadata(), 0L, token).ConfigureAwait(false);
            await base.FlushAsync(token).ConfigureAwait(false);
        }

        internal ValueTask WriteMetadataAsync(long index, DateTimeOffset timestamp, long term, CancellationToken token = default)
        {
            metadata = new(index, timestamp, term, FileSize - fileOffset);
            return RandomAccess.WriteAsync(Handle, SerializeMetadata(), 0L, token);
        }

        internal override async ValueTask WriteAsync<TEntry>(TEntry entry, long index, CancellationToken token = default)
        {
            // write snapshot
            await SetWritePositionAsync(fileOffset, token).ConfigureAwait(false);
            await entry.WriteToAsync(writer, token).ConfigureAwait(false);

            // update metadata
            metadata = SnapshotMetadata.Create(entry, index, writer.WritePosition - fileOffset);
        }

        // optimization hint is not supported for snapshots
        internal LogEntry Read(int sessionId)
            => new LogEntry(GetSessionReader(sessionId), in metadata);

        // cached index of the snapshotted entry
        public ref readonly SnapshotMetadata Metadata => ref metadata;

        ValueTask<IAsyncBinaryReader> ISnapshotReader.BeginReadSnapshotAsync(int sessionId, CancellationToken token)
            => token.IsCancellationRequested ? ValueTask.FromCanceled<IAsyncBinaryReader>(token) : new(GetSessionReader(sessionId));

        void ISnapshotReader.EndReadSnapshot()
        {
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                metadataBuffer.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Represents snapshot building context.
    /// </summary>
    /// <remarks>
    /// This type contains internal data needed to initialize
    /// the snapshot builder.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    protected readonly struct SnapshotBuilderContext
    {
        internal readonly Snapshot Snapshot;

        internal SnapshotBuilderContext(Snapshot snapshot, MemoryAllocator<byte> allocator)
        {
            Snapshot = snapshot;
            Allocator = allocator;
        }

        /// <summary>
        /// Gets the buffer allocator.
        /// </summary>
        public MemoryAllocator<byte> Allocator { get; }
    }

    /// <summary>
    /// Represents snapshot builder.
    /// </summary>
    protected abstract class SnapshotBuilder : Disposable
    {
        internal readonly SnapshotBuilderContext Context;
        private protected readonly DateTimeOffset Timestamp;

        private protected SnapshotBuilder(in SnapshotBuilderContext context)
        {
            Context = context;
            Timestamp = DateTimeOffset.UtcNow;
        }

        internal abstract ValueTask InitializeAsync(int sessionId);

        internal abstract ValueTask BuildAsync(long snapshotIndex);

        internal long Term
        {
            private protected get;
            set;
        }

        /// <summary>
        /// Interprets the command specified by the log entry.
        /// </summary>
        /// <param name="entry">The committed log entry.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        protected internal abstract ValueTask ApplyAsync(LogEntry entry);

        /// <summary>
        /// Allows to adjust the index of the current log entry to be snapshotted.
        /// </summary>
        /// <remarks>
        /// If <paramref name="currentIndex"/> is modified in a way when it out of bounds
        /// then snapshot process will be terminated immediately. Moreover,
        /// compaction algorithm is optimized for monothonic growth of this index.
        /// Stepping back or random access may slow down the process.
        /// </remarks>
        /// <param name="startIndex">The lower bound of the index, inclusive.</param>
        /// <param name="endIndex">The upper bound of the index, inclusive.</param>
        /// <param name="currentIndex">The currently running index.</param>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        protected internal virtual void AdjustIndex(long startIndex, long endIndex, ref long currentIndex)
        {
        }
    }

    /// <summary>
    /// Represents incremental snapshot builder.
    /// </summary>
    protected abstract class IncrementalSnapshotBuilder : SnapshotBuilder, IRaftLogEntry
    {
        /// <summary>
        /// Initializes a new snapshot builder.
        /// </summary>
        /// <param name="context">The context of the snapshot builder.</param>
        protected IncrementalSnapshotBuilder(in SnapshotBuilderContext context)
            : base(context)
        {
        }

        /// <inheritdoc/>
        long? IDataTransferObject.Length => null;

        /// <inheritdoc/>
        long IRaftLogEntry.Term => Term;

        /// <inheritdoc/>
        DateTimeOffset ILogEntry.Timestamp => Timestamp;

        /// <inheritdoc/>
        bool IDataTransferObject.IsReusable => false;

        /// <inheritdoc/>
        bool ILogEntry.IsSnapshot => true;

        /// <summary>
        /// Serializes the snapshotted entry.
        /// </summary>
        /// <param name="writer">The binary writer.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <typeparam name="TWriter">The type of binary writer.</typeparam>
        /// <returns>The task representing state of asynchronous execution.</returns>
        public abstract ValueTask WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            where TWriter : IAsyncBinaryWriter;

        internal sealed override ValueTask InitializeAsync(int sessionId)
            => Context.Snapshot.IsEmpty ? ValueTask.CompletedTask : ApplyAsync(Context.Snapshot.Read(sessionId));

        internal sealed override async ValueTask BuildAsync(long snapshotIndex)
        {
            await Context.Snapshot.WriteAsync(this, snapshotIndex).ConfigureAwait(false);
            await Context.Snapshot.FlushAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Represents a snapshot builder that allows to write directly to the snapshot file
    /// using <see cref="RandomAccess"/> class.
    /// </summary>
    protected abstract class InlineSnapshotBuilder : SnapshotBuilder
    {
        /// <summary>
        /// Gets the offset from the start of the snapshot file that is reserved
        /// and should not be used for storing data.
        /// </summary>
        protected static long SnapshotOffset => SnapshotMetadata.Size;

        /// <summary>
        /// Initializes a new snapshot builder.
        /// </summary>
        /// <param name="context">The context of the snapshot builder.</param>
        protected InlineSnapshotBuilder(in SnapshotBuilderContext context)
            : base(context)
        {
        }

        /// <summary>
        /// Gets the file descriptor pointing to the snapshot file.
        /// </summary>
        /// <seealso cref="SnapshotOffset"/>
        protected SafeFileHandle SnapshotFileHandle => Context.Snapshot.Handle;

        /// <summary>
        /// Flushes the data to the snapshot file.
        /// </summary>
        /// <returns>The task representing asynchronous result.</returns>
        protected virtual ValueTask FlushAsync() => ValueTask.CompletedTask;

        /// <summary>
        /// Initializes the builder.
        /// </summary>
        /// <returns>The task representing asynchronous result.</returns>
        protected virtual ValueTask InitializeAsync() => ValueTask.CompletedTask;

        internal sealed override ValueTask InitializeAsync(int sessionId) => InitializeAsync();

        internal sealed override async ValueTask BuildAsync(long snapshotIndex)
        {
            await FlushAsync().ConfigureAwait(false);

            // write metadata and invalidate internal buffers because the snapshot file has been modified directly
            await Context.Snapshot.WriteMetadataAsync(snapshotIndex, Timestamp, Term).ConfigureAwait(false);
            Context.Snapshot.Invalidate();
        }
    }

    /// <summary>
    /// Creates a new snapshot builder.
    /// </summary>
    /// <param name="context">The context of the snapshot builder.</param>
    /// <returns>The snapshot builder; or <see langword="null"/> if snapshotting is not supported.</returns>
    protected abstract SnapshotBuilder CreateSnapshotBuilder(in SnapshotBuilderContext context);

    private SnapshotBuilder CreateSnapshotBuilder()
        => CreateSnapshotBuilder(new SnapshotBuilderContext(snapshot, bufferManager.BufferAllocator));

    private protected sealed override ISnapshotReader? SnapshotReader
        => snapshot.IsEmpty ? null : snapshot;
}