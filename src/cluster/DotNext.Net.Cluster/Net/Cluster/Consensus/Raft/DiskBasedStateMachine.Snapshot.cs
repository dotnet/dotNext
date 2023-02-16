using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using static System.Globalization.CultureInfo;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using static Threading.AtomicInt64;
using IAsyncBinaryReader = IO.IAsyncBinaryReader;

public partial class DiskBasedStateMachine
{
    /// <summary>
    /// Represents a token that uniquely identifies the concurrent read of the snapshot.
    /// </summary>
    /// <remarks>
    /// This token is suitable to act as a key in the dictionary to keep short-living
    /// data (such as opened descriptors and other resources) associated with snapshot read operation.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    protected readonly record struct SnapshotAccessToken : IEquatable<SnapshotAccessToken>
    {
        private readonly int id;

        internal SnapshotAccessToken(int sessionId) => id = sessionId;

        /// <summary>
        /// Converts the token to string for debugging purposes.
        /// </summary>
        /// <returns>A string that represent this token.</returns>
        public override string ToString() => id.ToString(InvariantCulture);
    }

    /// <summary>
    /// Rewrites local snapshot with the snapshot supplied by remote cluster member.
    /// </summary>
    /// <typeparam name="TSnapshot">The type of the log entry containing snapshot.</typeparam>
    /// <param name="snapshot">The log entry containing snapshot.</param>
    /// <returns>The size of the snapshot, in bytes.</returns>
    protected abstract ValueTask<long> InstallSnapshotAsync<TSnapshot>(TSnapshot snapshot)
        where TSnapshot : notnull, IRaftLogEntry;

    private protected sealed override async ValueTask InstallSnapshotAsync<TSnapshot>(TSnapshot snapshot, long snapshotIndex)
    {
        Debug.Assert(snapshot.IsSnapshot);

        var snapshotLength = await InstallSnapshotAsync(snapshot).ConfigureAwait(false);
        LastCommittedEntryIndex = snapshotIndex;
        LastUncommittedEntryIndex = Math.Max(snapshotIndex, LastUncommittedEntryIndex);
        lastTerm.VolatileWrite(snapshot.Term);
        LastAppliedEntryIndex = snapshotIndex;
        UpdateSnapshotInfo(SnapshotMetadata.Create(snapshot, snapshotIndex, snapshotLength));
        await PersistInternalStateAsync(InternalStateScope.IndexesAndSnapshot).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts the reading of a snapshot.
    /// </summary>
    /// <param name="session">
    /// The internal identifier of the read session. Multiple reads can happen in the same time (in parallel),
    /// thus it is possible to use this identifier to distinguish them.
    /// </param>
    /// <param name="allocator">The memory allocator that can be used to rent the buffer for the reader.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The snapshot reader.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    protected abstract ValueTask<IAsyncBinaryReader> BeginReadSnapshotAsync(SnapshotAccessToken session, MemoryAllocator<byte> allocator, CancellationToken token);

    private protected sealed override ValueTask<IAsyncBinaryReader> BeginReadSnapshotAsync(int sessionId, CancellationToken token)
        => BeginReadSnapshotAsync(new SnapshotAccessToken(sessionId), bufferManager.BufferAllocator, token);

    /// <summary>
    /// Ends the reading of a snapshot.
    /// </summary>
    /// <param name="session">A token that uniquely identifies the concurrent read of the snapshot.</param>
    protected abstract void EndReadSnapshot(SnapshotAccessToken session);

    private protected sealed override void EndReadSnapshot(int sessionId)
        => EndReadSnapshot(new SnapshotAccessToken(sessionId));
}