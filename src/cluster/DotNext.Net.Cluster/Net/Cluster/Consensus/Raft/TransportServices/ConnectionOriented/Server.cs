using System.Buffers.Binary;
using System.Net;
using System.Runtime.CompilerServices;
using DotNext.IO.Log;
using Microsoft.Extensions.Logging;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using Buffers;

internal abstract partial class Server : Disposable, IServer
{
    private readonly ILocalMember localMember;
    private protected readonly ILogger logger;

    private protected Server(EndPoint address, ILocalMember localMember, ILoggerFactory loggerFactory)
    {
        Debug.Assert(address is not null);
        Debug.Assert(localMember is not null);
        Debug.Assert(loggerFactory is not null);

        Address = address;
        this.localMember = localMember;
        logger = loggerFactory.CreateLogger(GetType());
    }

    public abstract TimeSpan ReceiveTimeout
    {
        get;
        init;
    }

    public EndPoint Address { get; }

    private protected abstract MemoryAllocator<byte> BufferAllocator { get; }

    public abstract ValueTask StartAsync(CancellationToken token);

    private protected ValueTask ProcessRequestAsync(MessageType type, ProtocolStream protocol, CancellationToken token) => type switch
    {
        MessageType.Vote => VoteAsync(protocol, token),
        MessageType.PreVote => PreVoteAsync(protocol, token),
        MessageType.Synchronize => SynchronizeAsync(protocol, token),
        MessageType.Metadata => GetMetadataAsync(protocol, token),
        MessageType.Resign => ResignAsync(protocol, token),
        MessageType.InstallSnapshot => InstallSnapshotAsync(protocol, token),
        MessageType.AppendEntries => AppendEntriesAsync(protocol, token),
        _ => ValueTask.FromException(new InvalidOperationException(ExceptionMessages.UnknownRaftMessageType(type))),
    };

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask VoteAsync(ProtocolStream protocol, CancellationToken token)
    {
        await protocol.ReadAsync(VoteMessage.Size, token).ConfigureAwait(false);
        var response = await Invoke(localMember, protocol.WrittenBufferSpan, token).ConfigureAwait(false);
        protocol.Reset();
        await protocol.WriteBoolResultAsync(in response, token).ConfigureAwait(false);

        static ValueTask<Result<bool>> Invoke(ILocalMember localMember, ReadOnlySpan<byte> requestData, CancellationToken token)
        {
            var request = VoteMessage.Parse(requestData);
            return localMember.VoteAsync(request.Id, request.Term, request.LastLogIndex, request.LastLogTerm, token);
        }
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask PreVoteAsync(ProtocolStream protocol, CancellationToken token)
    {
        await protocol.ReadAsync(PreVoteMessage.Size, token).ConfigureAwait(false);
        var response = await Invoke(localMember, protocol.WrittenBufferSpan, token).ConfigureAwait(false);
        protocol.Reset();
        await protocol.WritePreVoteResultAsync(in response, token).ConfigureAwait(false);

        static ValueTask<Result<PreVoteResult>> Invoke(ILocalMember localMember, ReadOnlySpan<byte> requestData, CancellationToken token)
        {
            var request = PreVoteMessage.Parse(requestData);
            return localMember.PreVoteAsync(request.Id, request.Term, request.LastLogIndex, request.LastLogTerm, token);
        }
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask SynchronizeAsync(ProtocolStream protocol, CancellationToken token)
    {
        await protocol.ReadAsync(sizeof(long), token).ConfigureAwait(false);
        var response = await localMember.SynchronizeAsync(BinaryPrimitives.ReadInt64LittleEndian(protocol.WrittenBufferSpan), token).ConfigureAwait(false);
        protocol.Reset();
        await protocol.WriteNullableInt64Async(in response, token).ConfigureAwait(false);
    }

    private async ValueTask GetMetadataAsync(ProtocolStream protocol, CancellationToken token)
    {
        using var buffer = BufferAllocator(length: 512);
        protocol.Reset();
        await protocol.WriteDictionaryAsync(localMember.Metadata, buffer.Memory, token).ConfigureAwait(false);
    }

    private async ValueTask ResignAsync(ProtocolStream protocol, CancellationToken token)
    {
        var response = await localMember.ResignAsync(token).ConfigureAwait(false);
        protocol.Reset();
        await protocol.WriteBoolAsync(response, token).ConfigureAwait(false);
    }

    private async ValueTask InstallSnapshotAsync(ProtocolStream protocol, CancellationToken token)
    {
        await protocol.ReadAsync(SnapshotMessage.Size, token).ConfigureAwait(false);

        var snapshot = new ReceivedSnapshot(protocol);
        
        // read configuration first
        var configuration = new ProtocolStreamSegment(protocol);
        await localMember
            .InstallConfigurationAsync(
                snapshot.Message.Term,
                configuration,
                snapshot.Message.ConfigurationVersion,
                token)
            .ConfigureAwait(false);

        // skip contents of the configuration if it wasn't consumed
        await configuration.EnsureConsumedAsync(token).ConfigureAwait(false);
        
        protocol.ResetReadState();
        var response = await localMember.InstallSnapshotAsync(
            snapshot.Message.Id,
            snapshot.Message.Term,
            snapshot,
            snapshot.Message.SnapshotIndex,
            token).ConfigureAwait(false);

        // skip contents of the snapshot if it wasn't consumed
        await snapshot.EnsureConsumedAsync(token).ConfigureAwait(false);
        protocol.Reset();
        
        await protocol.WriteHeartbeatResultAsync(in response, token).ConfigureAwait(false);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask AppendEntriesAsync(ProtocolStream protocol, CancellationToken token)
    {
        await protocol.ReadAsync(AppendEntriesMessage.Size, token).ConfigureAwait(false);
        var response = await AppendEntriesAsync(localMember, out var enumerator, protocol, token).ConfigureAwait(false);
        try
        {
            // skip remaining log entries
            while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                await protocol.SkipAsync(token).ConfigureAwait(false);
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }

        protocol.Reset();
        await protocol.WriteHeartbeatResultAsync(in response, token).ConfigureAwait(false);
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ValueTask<Result<HeartbeatResult>> AppendEntriesAsync(ILocalMember localMember, out IAsyncEnumerator<IRaftLogEntry> enumerator, ProtocolStream protocol,
        CancellationToken token)
    {
        var reader = new SpanReader<byte>(protocol.WrittenBufferSpan);
        var message = reader.Read<AppendEntriesMessage>();
        protocol.AdvanceReadCursor(reader.ConsumedCount);

        var entries = message.EntriesCount > 0
            ? new ReceivedLogEntries(protocol, message.EntriesCount, token)
            : ILogEntryProducer<IRaftLogEntry>.Empty;

        enumerator = entries;
        return localMember.AppendEntriesAsync(message.Id,
            message.Term,
            entries,
            message.PrevLogIndex,
            message.PrevLogTerm, message.CommitIndex,
            token);
    }

    public new ValueTask DisposeAsync() => base.DisposeAsync();
}