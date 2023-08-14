using System.Buffers.Binary;
using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using Buffers;
using IO;

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
            var reader = new SpanReader<byte>(requestData);
            var request = VoteMessage.Read(ref reader);
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
            var reader = new SpanReader<byte>(requestData);
            var request = PreVoteMessage.Read(ref reader);
            return localMember.PreVoteAsync(request.Id, request.Term, request.LastLogIndex, request.LastLogTerm, token);
        }
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask SynchronizeAsync(ProtocolStream protocol, CancellationToken token)
    {
        await protocol.ReadAsync(sizeof(long), token).ConfigureAwait(false);
        var response = await localMember.SynchronizeAsync(BinaryPrimitives.ReadInt32LittleEndian(protocol.WrittenBufferSpan), token).ConfigureAwait(false);
        protocol.Reset();
        await protocol.WriteNullableInt64Async(in response, token).ConfigureAwait(false);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask GetMetadataAsync(ProtocolStream protocol, CancellationToken token)
    {
        using var buffer = BufferAllocator(length: 512);
        protocol.Reset();
        await protocol.WriteDictionaryAsync(localMember.Metadata, buffer.Memory, token).ConfigureAwait(false);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask ResignAsync(ProtocolStream protocol, CancellationToken token)
    {
        var response = await localMember.ResignAsync(token).ConfigureAwait(false);
        protocol.Reset();
        await protocol.WriteBoolAsync(response, token).ConfigureAwait(false);
    }

    private async ValueTask InstallSnapshotAsync(ProtocolStream protocol, CancellationToken token)
    {
        Result<HeartbeatResult> response;
        await protocol.ReadAsync(SnapshotMessage.Size, token).ConfigureAwait(false);
        using (var snapshot = new ReceivedSnapshot(protocol))
        {
            protocol.AdvanceReadCursor(SnapshotMessage.Size);
            response = await localMember.InstallSnapshotAsync(snapshot.Id, snapshot.Term, snapshot, snapshot.Index, token).ConfigureAwait(false);
        }

        if (response.Value is HeartbeatResult.Rejected)
        {
            // skip contents of snapshot
            await protocol.SkipAsync(token).ConfigureAwait(false);
        }

        protocol.Reset();
        await protocol.WriteHeartbeatResultAsync(in response, token).ConfigureAwait(false);
    }

    private static int AppendEntriesHeadersSize => AppendEntriesMessage.Size + sizeof(byte) + sizeof(long) + sizeof(long);

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask AppendEntriesAsync(ProtocolStream protocol, CancellationToken token)
    {
        Result<HeartbeatResult> response;

        await protocol.ReadAsync(AppendEntriesHeadersSize, token).ConfigureAwait(false);
        using (var entries = new ReceivedLogEntries(protocol, BufferAllocator, token))
        {
            protocol.AdvanceReadCursor(AppendEntriesHeadersSize);

            // read configuration
            var configuration = entries.Configuration.Content;
            if (!configuration.IsEmpty)
            {
                await protocol.ReadBlockAsync(configuration, token).ConfigureAwait(false);
            }

            protocol.ResetReadState();
            response = await localMember.AppendEntriesAsync(entries.Id, entries.Term, entries, entries.PrevLogIndex, entries.PrevLogTerm, entries.CommitIndex, entries.Configuration, entries.ApplyConfig, token).ConfigureAwait(false);

            // skip remaining log entries
            while (await entries.MoveNextAsync().ConfigureAwait(false))
                await protocol.SkipAsync(token).ConfigureAwait(false);
        }

        protocol.Reset();
        await protocol.WriteHeartbeatResultAsync(in response, token).ConfigureAwait(false);
    }

    public new ValueTask DisposeAsync() => base.DisposeAsync();
}