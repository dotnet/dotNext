using System.Buffers;
using System.Collections.Immutable;
using static System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.Net.Cluster.Consensus.Raft.Membership;

using Buffers;
using IO;

/// <summary>
/// Represents persistent cluster configuration storage.
/// </summary>
/// <typeparam name="TAddress">The type of the cluster member address.</typeparam>
public abstract class PersistentClusterConfigurationStorage<TAddress> : ClusterConfigurationStorage<TAddress>, IAsyncDisposable
    where TAddress : notnull
{
    private const string ActiveConfigurationFileName = "active.list";
    private const string ProposedConfigurationFileName = "proposed.list";

    private sealed class ClusterConfiguration : Disposable, IClusterConfiguration, IAsyncDisposable
    {
        internal const int PayloadOffset = sizeof(long);

        // first 8 bytes reserved for fingerprint
        private readonly FileStream fs;
        private readonly MemoryAllocator<byte>? allocator;
        private readonly int bufferSize;

        internal ClusterConfiguration(string fileName, int fileBufferSize, MemoryAllocator<byte>? allocator)
        {
            this.allocator = allocator;
            bufferSize = fileBufferSize;
            fs = new(fileName, new FileStreamOptions
            {
                Mode = FileMode.OpenOrCreate,
                Access = FileAccess.ReadWrite,
                Share = FileShare.Read,
                BufferSize = fileBufferSize,
                Options = FileOptions.SequentialScan | FileOptions.Asynchronous | FileOptions.WriteThrough,
            });
        }

        public long Fingerprint { get; set; }

        public long Length => Math.Max(fs.Length - PayloadOffset, 0L);

        internal bool IsEmpty => Length == 0L;

        bool IDataTransferObject.IsReusable => true;

        internal async ValueTask UpdateAsync(ReadOnlyMemory<byte> content, CancellationToken token)
        {
            fs.Position = 0L;
            fs.SetLength(content.Length);
            await fs.WriteAsync(content, token).ConfigureAwait(false);
            await fs.FlushAsync(token).ConfigureAwait(false);
        }

        internal void Clear()
        {
            Fingerprint = 0L;
            fs.SetLength(0L);
            fs.Flush(flushToDisk: true);
        }

        internal async ValueTask CopyToAsync(ClusterConfiguration output, CancellationToken token)
        {
            output.fs.Position = 0L;
            output.Fingerprint = Fingerprint;
            fs.Position = 0L;
            await fs.CopyToAsync(output.fs, bufferSize, token).ConfigureAwait(false);
            await output.fs.FlushAsync(token).ConfigureAwait(false);
            output.fs.SetLength(fs.Length);
        }

        internal Task CopyToAsync(IBufferWriter<byte> output, CancellationToken token)
            => fs.CopyToAsync(output, token: token);

        async ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        {
            // this method should be safe for concurrent invocations
            var handle = fs.SafeFileHandle;
            using var buffer = allocator.Invoke(bufferSize, exactSize: false);

            for (int offset = PayloadOffset, count; (count = await RandomAccess.ReadAsync(handle, buffer.Memory, offset, token).ConfigureAwait(false)) > 0; offset += count)
            {
                await writer.Invoke(buffer.Memory.Slice(0, count), token).ConfigureAwait(false);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                fs.Dispose();

            base.Dispose(disposing);
        }

        protected override ValueTask DisposeAsyncCore() => fs.DisposeAsync();

        public new ValueTask DisposeAsync() => base.DisposeAsync();
    }

    private readonly ClusterConfiguration active, proposed;
    private readonly IEqualityComparer<TAddress>? comparer;
    private readonly int bufferSize;

    /// <summary>
    /// Initializes a new persistent storage.
    /// </summary>
    /// <param name="path">The full path to the folder used as persistent storage of cluster members.</param>
    /// <param name="fileBufferSize">The buffer size for file I/O.</param>
    /// <param name="comparer">The address comparer.</param>
    /// <param name="allocator">The memory allocator for file I/O.</param>
    protected PersistentClusterConfigurationStorage(string path, int fileBufferSize = 512, IEqualityComparer<TAddress>? comparer = null, MemoryAllocator<byte>? allocator = null)
        : this(new DirectoryInfo(path), fileBufferSize, comparer, allocator)
    {
        this.comparer = comparer;
    }

    private PersistentClusterConfigurationStorage(DirectoryInfo storage, int fileBufferSize, IEqualityComparer<TAddress>? comparer, MemoryAllocator<byte>? allocator)
        : base(comparer, allocator)
    {
        if (!storage.Exists)
            storage.Create();

        active = new(Path.Combine(storage.FullName, ActiveConfigurationFileName), fileBufferSize, allocator);
        proposed = new(Path.Combine(storage.FullName, ProposedConfigurationFileName), fileBufferSize, allocator);
        bufferSize = fileBufferSize;
    }

    /// <inheritdoc />
    public sealed override bool HasProposal => !proposed.IsEmpty;

    /// <summary>
    /// Gets active configuration.
    /// </summary>
    public sealed override IClusterConfiguration ActiveConfiguration => active;

    /// <summary>
    /// Gets proposed configuration.
    /// </summary>
    public sealed override IClusterConfiguration? ProposedConfiguration => proposed.IsEmpty ? null : proposed;

    /// <inheritdoc/>
    protected sealed override async ValueTask ProposeAsync(IClusterConfiguration configuration, CancellationToken token = default)
    {
        using var writer = new PooledBufferWriter<byte>(allocator) { Capacity = bufferSize };
        writer.WriteInt64(configuration.Fingerprint, littleEndian: true);
        await configuration.WriteToAsync(writer, token).ConfigureAwait(false);

        proposed.Fingerprint = configuration.Fingerprint;
        await proposed.UpdateAsync(writer.WrittenMemory, token).ConfigureAwait(false);

        proposedCache.Clear();
        var builder = proposedCache.ToBuilder();
        Decode(builder, writer.WrittenMemory.Slice(ClusterConfiguration.PayloadOffset));
        proposedCache = builder.ToImmutable();
        Interlocked.MemoryBarrierProcessWide();
    }

    /// <inheritdoc/>
    protected sealed override ValueTask ApplyAsync(CancellationToken token = default)
        => proposed.IsEmpty ? ValueTask.CompletedTask : ApplyProposedAsync(token);

    private async ValueTask ApplyProposedAsync(CancellationToken token)
    {
        await proposed.CopyToAsync(active, token).ConfigureAwait(false);
        await CompareAsync(activeCache, proposedCache, token).ConfigureAwait(false);
        activeCache = proposedCache;

        proposed.Clear();
        proposedCache = proposedCache.Clear();

        Interlocked.MemoryBarrierProcessWide();
        OnActivated();
    }

    /// <inheritdoc/>
    protected sealed override async ValueTask LoadConfigurationAsync(CancellationToken token = default)
    {
        var builder = ImmutableHashSet.CreateBuilder(comparer);

        using var buffer = new PooledBufferWriter<byte>(allocator)
        {
            Capacity = active.IsEmpty ? bufferSize : int.CreateSaturating(active.Length),
        };

        // restore active configuration
        if (!active.IsEmpty)
        {
            await active.CopyToAsync(buffer, token).ConfigureAwait(false);
            active.Fingerprint = ReadInt64LittleEndian(buffer.WrittenMemory.Span);
            Decode(builder, buffer.WrittenMemory.Slice(ClusterConfiguration.PayloadOffset));
            activeCache = builder.ToImmutable();
        }

        buffer.Clear(true);
        builder.Clear();

        // restore proposed configuration
        if (!proposed.IsEmpty)
        {
            await proposed.CopyToAsync(buffer, token).ConfigureAwait(false);
            proposed.Fingerprint = ReadInt64LittleEndian(buffer.WrittenMemory.Span);
            Decode(builder, buffer.WrittenMemory.Slice(sizeof(long)));
            proposedCache = builder.ToImmutable();
        }

        Interlocked.MemoryBarrierProcessWide();
        builder.Clear();
    }

    private MemoryOwner<byte> Encode(IReadOnlyCollection<TAddress> configuration, long fingerprint)
    {
        MemoryOwner<byte> result;
        var writer = new BufferWriterSlim<byte>(bufferSize, allocator);

        try
        {
            writer.WriteLittleEndian(fingerprint);
            Encode(configuration, ref writer);

            if (!writer.TryDetachBuffer(out result))
                result = writer.WrittenSpan.Copy(allocator);
        }
        finally
        {
            writer.Dispose();
        }

        return result;
    }

    /// <inheritdoc />
    protected sealed override async ValueTask<bool> AddMemberAsync(TAddress address, CancellationToken token = default)
    {
        if (!proposed.IsEmpty || activeCache.Contains(address))
            return false;

        var builder = activeCache.ToBuilder();
        builder.Add(address);
        proposedCache = builder.ToImmutable();

        proposed.Fingerprint = GenerateFingerprint();
        using (var buffer = Encode(builder, proposed.Fingerprint))
            await proposed.UpdateAsync(buffer.Memory, token).ConfigureAwait(false);

        Interlocked.MemoryBarrierProcessWide();
        builder.Clear();

        return true;
    }

    /// <inheritdoc />
    protected sealed override async ValueTask<bool> RemoveMemberAsync(TAddress address, CancellationToken token = default)
    {
        if (!proposed.IsEmpty)
            return false;

        var builder = activeCache.ToBuilder();
        if (!builder.Remove(address))
            return false;
        proposedCache = builder.ToImmutable();

        proposed.Fingerprint = GenerateFingerprint();
        using (var buffer = Encode(builder, proposed.Fingerprint))
            await proposed.UpdateAsync(buffer.Memory, token).ConfigureAwait(false);

        Interlocked.MemoryBarrierProcessWide();
        builder.Clear();

        return true;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            active.Dispose();
            proposed.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        await active.DisposeAsync().ConfigureAwait(false);
        await proposed.DisposeAsync().ConfigureAwait(false);
        await base.DisposeAsyncCore().ConfigureAwait(false);
    }

    /// <summary>
    /// Releases managed resources associated with this object.
    /// </summary>
    /// <returns>The task representing asynchronous result.</returns>
    public new ValueTask DisposeAsync() => base.DisposeAsync();
}