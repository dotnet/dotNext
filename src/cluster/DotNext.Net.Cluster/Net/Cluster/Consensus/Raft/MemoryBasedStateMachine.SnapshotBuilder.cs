using System.ComponentModel;
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
    internal sealed class Snapshot : ConcurrentStorageAccess
    {
        private new const string FileName = "snapshot";

        internal Snapshot(DirectoryInfo? location, int bufferSize, in BufferManager manager, int readersCount, WriteMode writeMode, long initialSize = 0L)
            : base(GetPath(location), 0, bufferSize, manager.BufferAllocator, readersCount, writeMode, initialSize)
        {
        }

        private static string GetPath(DirectoryInfo? location)
        {
            var (directory, fileName) = location is null
                ? (Path.GetTempPath(), Path.GetRandomFileName())
                : (location.FullName, FileName);

            return Path.Combine(directory, fileName);
        }

        internal async ValueTask<long> WriteAsync<TEntry>(TEntry entry, CancellationToken token = default)
            where TEntry : IRaftLogEntry
        {
            // write snapshot
            await SetWritePositionAsync(fileOffset, token).ConfigureAwait(false);
            await entry.WriteToAsync(writer, token).ConfigureAwait(false);

            // compute actual length of the snapshot
            return writer.WritePosition - fileOffset;
        }

        internal IAsyncBinaryReader this[int sessionId] => GetSessionReader(sessionId);
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

        private protected SnapshotBuilder(in SnapshotBuilderContext context)
        {
            Context = context;
            Timestamp = DateTimeOffset.UtcNow;
        }

        internal abstract ValueTask InitializeAsync(int sessionId, SnapshotMetadata metadata);

        internal abstract ValueTask<SnapshotMetadata> BuildAsync(long snapshotIndex);

        internal long Term
        {
            private protected get;
            set;
        }

        private protected DateTimeOffset Timestamp { get; private set; }

        internal void RefreshTimestamp() => Timestamp = DateTimeOffset.UtcNow;

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
        /// compaction algorithm is optimized for monotonic growth of this index.
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

        internal sealed override ValueTask InitializeAsync(int sessionId, SnapshotMetadata metadata)
            => metadata.Index > 0L ? ApplyAsync(new(in metadata) { ContentReader = Context.Snapshot[sessionId] }) : ValueTask.CompletedTask;

        internal sealed override async ValueTask<SnapshotMetadata> BuildAsync(long snapshotIndex)
        {
            var snapshotLength = await Context.Snapshot.WriteAsync(this).ConfigureAwait(false);
            await Context.Snapshot.FlushAsync().ConfigureAwait(false);
            return SnapshotMetadata.Create(this, snapshotIndex, snapshotLength);
        }
    }

    /// <summary>
    /// Represents a snapshot builder that allows to write directly to the snapshot file
    /// using <see cref="RandomAccess"/> class.
    /// </summary>
    protected abstract class InlineSnapshotBuilder : SnapshotBuilder
    {
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
        protected SafeFileHandle SnapshotFileHandle => Context.Snapshot.Handle;

        /// <summary>
        /// Flushes the data to the snapshot file.
        /// </summary>
        /// <returns>The size of the snapshot, in bytes. It can be less than the size of the file.</returns>
        protected virtual ValueTask<long> FlushAsync() => new(Context.Snapshot.FileSize);

        /// <summary>
        /// Initializes the builder.
        /// </summary>
        /// <returns>The task representing asynchronous result.</returns>
        protected virtual ValueTask InitializeAsync() => ValueTask.CompletedTask;

        internal sealed override ValueTask InitializeAsync(int sessionId, SnapshotMetadata metadata)
            => InitializeAsync();

        internal sealed override async ValueTask<SnapshotMetadata> BuildAsync(long snapshotIndex)
        {
            var snapshotLength = await FlushAsync().ConfigureAwait(false);

            // invalidate internal buffers because the snapshot file has been modified directly
            Context.Snapshot.Invalidate();
            return new SnapshotMetadata(snapshotIndex, Timestamp, Term, snapshotLength);
        }
    }

    private sealed class LongLivingSnapshotBuilder : Disposable
    {
        internal readonly SnapshotBuilder Builder;
        internal long LastAppliedIndex;

        internal LongLivingSnapshotBuilder(SnapshotBuilder builder)
            => Builder = builder;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Builder.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Creates a new snapshot builder.
    /// </summary>
    /// <param name="context">The context of the snapshot builder.</param>
    /// <returns>The snapshot builder.</returns>
    protected abstract SnapshotBuilder CreateSnapshotBuilder(in SnapshotBuilderContext context);

    private SnapshotBuilder CreateSnapshotBuilder()
        => CreateSnapshotBuilder(new SnapshotBuilderContext(snapshot, bufferManager.BufferAllocator));

    private async ValueTask<LongLivingSnapshotBuilder> InitializeLongLivingSnapshotBuilderAsync(int session)
    {
        var result = new LongLivingSnapshotBuilder(CreateSnapshotBuilder());
        await result.Builder.InitializeAsync(session, SnapshotInfo).ConfigureAwait(false);
        result.LastAppliedIndex = SnapshotInfo.Index;
        result.Builder.Term = SnapshotInfo.RecordMetadata.Term;
        return result;
    }

    private protected sealed override ValueTask<IAsyncBinaryReader> BeginReadSnapshotAsync(int sessionId, CancellationToken token)
        => token.IsCancellationRequested ? ValueTask.FromCanceled<IAsyncBinaryReader>(token) : new(snapshot[sessionId]);

    private protected sealed override void EndReadSnapshot(int sessionId)
    {
        // Nothing to do here
    }
}