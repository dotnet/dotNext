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

        internal ClusterConfiguration(string fileName, int fileBufferSize)
        {
            fs = new(fileName, new FileStreamOptions
            {
                Mode = FileMode.OpenOrCreate,
                Access = FileAccess.ReadWrite,
                Share = FileShare.Read,
                BufferSize = fileBufferSize,
                Options = FileOptions.SequentialScan | FileOptions.Asynchronous,
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
            fs.Flush(true);
        }

        internal async ValueTask CopyToAsync(ClusterConfiguration output, int bufferSize, CancellationToken token)
        {
            output.fs.SetLength(Length);
            output.Fingerprint = Fingerprint;
            fs.Position = 0L;
            await fs.CopyToAsync(output.fs, bufferSize, token).ConfigureAwait(false);
            await output.fs.FlushAsync(token).ConfigureAwait(false);
        }

        internal Task CopyToAsync(IBufferWriter<byte> output, CancellationToken token)
            => fs.CopyToAsync(output, token: token);

        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        {
            fs.Position = PayloadOffset;
            return new(writer.CopyFromAsync(fs, token));
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
    private readonly int bufferSize;

    /// <summary>
    /// Initializes a new persistent storage.
    /// </summary>
    /// <param name="path">The full path to the folder used as persistent storage of cluster members.</param>
    /// <param name="fileBufferSize">The buffer size for file I/O.</param>
    /// <param name="allocator">The memory allocator for file I/O.</param>
    protected PersistentClusterConfigurationStorage(string path, int fileBufferSize = 512, MemoryAllocator<byte>? allocator = null)
        : this(new DirectoryInfo(path), fileBufferSize, allocator)
    {
    }

    private PersistentClusterConfigurationStorage(DirectoryInfo storage, int fileBufferSize, MemoryAllocator<byte>? allocator)
        : base(10, allocator)
    {
        if (!storage.Exists)
            storage.Create();

        active = new(Path.Combine(storage.FullName, ActiveConfigurationFileName), fileBufferSize);
        proposed = new(Path.Combine(storage.FullName, ProposedConfigurationFileName), fileBufferSize);
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

    /// <summary>
    /// Proposes the configuration.
    /// </summary>
    /// <param name="configuration">The proposed configuration.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    public sealed override async ValueTask ProposeAsync(IClusterConfiguration configuration, CancellationToken token = default)
    {
        using var writer = new PooledBufferWriter<byte> { BufferAllocator = allocator, Capacity = bufferSize };
        writer.WriteInt64(configuration.Fingerprint, littleEndian: true);
        await configuration.WriteToAsync(writer, token).ConfigureAwait(false);

        proposed.Fingerprint = configuration.Fingerprint;
        await proposed.UpdateAsync(writer.WrittenMemory, token).ConfigureAwait(false);

        proposedCache.Clear();
        var builder = proposedCache.ToBuilder();
        Decode(builder, writer.WrittenMemory.Slice(ClusterConfiguration.PayloadOffset));
        proposedCache = builder.ToImmutable();
    }

    /// <summary>
    /// Applies proposed configuration as active configuration.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    public sealed override async ValueTask ApplyAsync(CancellationToken token = default)
    {
        if (proposed.IsEmpty)
            return;

        await proposed.CopyToAsync(active, bufferSize, token).ConfigureAwait(false);
        await CompareAsync(activeCache, proposedCache).ConfigureAwait(false);
        activeCache = proposedCache;

        proposed.Clear();
        proposedCache = proposedCache.Clear();

        OnActivated();
    }

    /// <summary>
    /// Loads configuration from file system.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous result.</returns>
    public sealed override async ValueTask LoadConfigurationAsync(CancellationToken token = default)
    {
        var builder = ImmutableDictionary.CreateBuilder<ClusterMemberId, TAddress>();

        using var buffer = new PooledBufferWriter<byte>
        {
            BufferAllocator = allocator,
            Capacity = active.IsEmpty ? bufferSize : active.Length.Truncate()
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

        builder.Clear();

        // send notifications
        await base.LoadConfigurationAsync(token).ConfigureAwait(false);
    }

    private MemoryOwner<byte> Encode(IReadOnlyDictionary<ClusterMemberId, TAddress> configuration, long fingerprint)
    {
        MemoryOwner<byte> result;
        var writer = new BufferWriterSlim<byte>(bufferSize, allocator);

        try
        {
            writer.WriteInt64(fingerprint, true);
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
    public sealed override async ValueTask<bool> AddMemberAsync(ClusterMemberId id, TAddress address, CancellationToken token = default)
    {
        if (!proposed.IsEmpty || activeCache.ContainsKey(id))
            return false;

        var builder = activeCache.ToBuilder();
        builder.Add(id, address);
        proposedCache = builder.ToImmutable();

        proposed.Fingerprint = GenerateFingerprint();
        using (var buffer = Encode(builder, proposed.Fingerprint))
            await proposed.UpdateAsync(buffer.Memory, token).ConfigureAwait(false);

        builder.Clear();

        return true;
    }

    /// <inheritdoc />
    public sealed override async ValueTask<bool> RemoveMemberAsync(ClusterMemberId id, CancellationToken token = default)
    {
        if (!proposed.IsEmpty || !activeCache.ContainsKey(id))
            return false;

        var builder = activeCache.ToBuilder();
        if (!builder.Remove(id, out var address))
            return false;
        proposedCache = builder.ToImmutable();

        proposed.Fingerprint = GenerateFingerprint();
        using (var buffer = Encode(builder, proposed.Fingerprint))
            await proposed.UpdateAsync(buffer.Memory, token).ConfigureAwait(false);

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