using System.Buffers;
using System.Threading.Channels;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Buffers;
using Buffers.Binary;
using IO;
using Threading;

public abstract partial class PersistentState : Disposable, IAsyncDisposable
{
    private const string DataPagesLocationPrefix = "data";
    private const string MetadataPagesLocationPrefix = "metadata";

    private readonly MemoryAllocator<byte> bufferAllocator;
    private long lastEntryIndex;

    protected PersistentState(DirectoryInfo location, Options options)
    {
        CreateIfNeeded(location);

        lockManager = new(options.ConcurrencyLevel);
        bufferAllocator = options.Allocator ?? ArrayPool<byte>.Shared.ToAllocator();
        
        // page management
        {
            var pagesLocation = new DirectoryInfo(Path.Combine(location.FullName, DataPagesLocationPrefix));
            CreateIfNeeded(pagesLocation);
            dataPages = new(pagesLocation, options.PageSize);

            pagesLocation = new(Path.Combine(location.FullName, MetadataPagesLocationPrefix));
            CreateIfNeeded(pagesLocation);
            metadataPages = new(pagesLocation, Environment.SystemPageSize);
        }
        
        // cleaner
        {
            var channel = Channel.CreateBounded<long>(new BoundedChannelOptions(options.ConcurrencyLevel)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true,
                SingleReader = true,
                AllowSynchronousContinuations = false,
            });

            cleanupTrigger = channel.Writer;
            cleanupTask = CleanUpAsync(channel.Reader);
        }
        
        // appender
        {
            var channel = Channel.CreateBounded<long>(new BoundedChannelOptions(options.ConcurrencyLevel)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true,
                SingleReader = true,
                AllowSynchronousContinuations = false,
            });

            appendTrigger = channel.Writer;
            appenderTask = ApplyAsync(channel.Reader);
            applyEvent = new(options.ConcurrencyLevel);
        }

        // flusher
        {
            commitIndexState = new(location);
            lastEntryIndex = commitIndexState.Value;
            var channel = Channel.CreateBounded<long>(new BoundedChannelOptions(options.ConcurrencyLevel)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false,
            });

            flushTrigger = channel.Writer;
            flusherTask = FlushAsync(channel.Reader);
        }

        static void CreateIfNeeded(DirectoryInfo directory)
        {
            if (!directory.Exists)
                directory.Create();
        }
    }

    public async ValueTask<long> AppendAsync<TEntry>(TEntry entry, CancellationToken token = default)
        where TEntry : IRaftLogEntry
    {
        long currentIndex;
        await lockManager.AcquireAppendLockAsync(token).ConfigureAwait(false);
        try
        {
            currentIndex = lastEntryIndex + 1L;

            await WritePayloadAsync(entry, out var startAddress, token).ConfigureAwait(false);
            WriteMetadata(entry, currentIndex, startAddress);

            Volatile.Write(ref lastEntryIndex, currentIndex);
        }
        finally
        {
            lockManager.ReleaseAppendLock();
        }

        return currentIndex;
    }

    public async ValueTask CommitAsync(long endIndex, CancellationToken token)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(endIndex);
        
        // only one commit thread is allowed
        await lockManager.AcquireCommitLockAsync(token).ConfigureAwait(false);
        try
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(endIndex, Volatile.Read(in lastEntryIndex));

            await flushTrigger.WriteAsync(endIndex, token).ConfigureAwait(false);
            await appendTrigger.WriteAsync(endIndex, token).ConfigureAwait(false);
        }
        finally
        {
            lockManager.ReleaseCommitLock();
        }
    }

    private ValueTask WritePayloadAsync<TEntry>(TEntry entry, out ulong startAddress, CancellationToken token)
        where TEntry : IRaftLogEntry
    {
        startAddress = dataPages.LastWrittenAddress;

        ValueTask task;
        if (entry is ISupplier<MemoryAllocator<byte>, MemoryOwner<byte>>)
        {
            var buffer = ((ISupplier<MemoryAllocator<byte>, MemoryOwner<byte>>)entry).Invoke(bufferAllocator);
            try
            {
                dataPages.Write(buffer.Span);
            }
            finally
            {
                buffer.Dispose();
            }
            
            task = ValueTask.CompletedTask;
        }
        else if (!dataPages.TryEnsureCapacity(entry.Length))
        {
            task = WriteSlowAsync(dataPages, entry, bufferAllocator, token);
        }
        else if (entry is IBinaryLogEntry)
        {
            var length = (int)entry.Length.GetValueOrDefault();
            ((IBinaryLogEntry)entry).WriteTo(dataPages.GetSpan(length));
            dataPages.Advance(length);
            task = ValueTask.CompletedTask;
        }
        else
        {
            task = entry.WriteToAsync(dataPages, token);
        }

        return task;

        static async ValueTask WriteSlowAsync(IBufferWriter<byte> writer, TEntry entry, MemoryAllocator<byte> allocator, CancellationToken token)
        {
            const int bufferSize = 1024;
            var buffer = allocator.AllocateAtLeast(bufferSize);
            var stream = writer.AsStream();
            try
            {
                await entry.WriteToAsync(stream, buffer.Memory, token).ConfigureAwait(false);
            }
            finally
            {
                buffer.Dispose();
                await stream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private void WriteMetadata<TEntry>(TEntry entry, long index, ulong startAddress)
        where TEntry : IRaftLogEntry
    {
        var length = long.CreateChecked(dataPages.LastWrittenAddress - startAddress);
        metadataPages[index] = LogEntryMetadata.Create(entry, startAddress, length);
    }

    private void CleanUp()
    {
        Dispose<PageManager>([dataPages, metadataPages]);
        Dispose<QueuedSynchronizer>([lockManager, applyEvent]);
        lockManager.Dispose();
        commitIndexState.Dispose();
    }

    /// <inheritdoc/>
    protected override async ValueTask DisposeAsyncCore()
    {
        flushTrigger.TryComplete();
        await flusherTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        appendTrigger.TryComplete();
        await appenderTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        cleanupTrigger.TryComplete();
        await cleanupTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        CleanUp();
    }

    /// <inheritdoc cref="IAsyncDisposable.DisposeAsync()"/>
    public new ValueTask DisposeAsync() => base.DisposeAsync();
}