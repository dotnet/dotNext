using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using Buffers;

internal abstract class Server : Disposable, IServer
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

    private protected abstract MemoryOwner<byte> AllocateBuffer(int bufferSize);

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
        var request = await protocol.ReadVoteRequestAsync(token).ConfigureAwait(false);
        var response = await localMember.VoteAsync(request.Id, request.Term, request.LastLogIndex, request.LastLogTerm, token).ConfigureAwait(false);
        protocol.Reset();
        await protocol.WriteResponseAsync(in response, token).ConfigureAwait(false);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask PreVoteAsync(ProtocolStream protocol, CancellationToken token)
    {
        var request = await protocol.ReadPreVoteRequestAsync(token).ConfigureAwait(false);
        var response = await localMember.PreVoteAsync(request.Id, request.Term, request.LastLogIndex, request.LastLogTerm, token).ConfigureAwait(false);
        protocol.Reset();
        await protocol.WriteResponseAsync(in response, token).ConfigureAwait(false);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask SynchronizeAsync(ProtocolStream protocol, CancellationToken token)
    {
        var request = await protocol.ReadInt64Async(token).ConfigureAwait(false);
        var response = await localMember.SynchronizeAsync(request, token).ConfigureAwait(false);
        protocol.Reset();
        await protocol.WriteResponseAsync(in response, token).ConfigureAwait(false);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask GetMetadataAsync(ProtocolStream protocol, CancellationToken token)
    {
        protocol.Reset();
        using var buffer = AllocateBuffer(bufferSize: 512);
        await protocol.WriteMetadataResponseAsync(localMember.Metadata, buffer.Memory, token).ConfigureAwait(false);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask ResignAsync(ProtocolStream protocol, CancellationToken token)
    {
        protocol.Reset();
        var response = await localMember.ResignAsync(token).ConfigureAwait(false);
        await protocol.WriteResponseAsync(response, token).ConfigureAwait(false);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask InstallSnapshotAsync(ProtocolStream protocol, CancellationToken token)
    {
        var request = await protocol.ReadInstallSnapshotRequestAsync(token).ConfigureAwait(false);
        Result<bool> response;
        using (request.Snapshot)
        {
            response = await localMember.InstallSnapshotAsync(request.Id, request.Term, request.Snapshot, request.SnapshotIndex, token).ConfigureAwait(false);
        }

        if (!response.Value)
        {
            // skip contents of snapshot
            await protocol.SkipAsync(token).ConfigureAwait(false);
        }

        protocol.Reset();
        await protocol.WriteResponseAsync(in response, token).ConfigureAwait(false);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask AppendEntriesAsync(ProtocolStream protocol, CancellationToken token)
    {
        var request = await protocol.ReadAppendEntriesRequestAsync(token).ConfigureAwait(false);
        Result<bool> response;
        using (request.Entries)
        {
            using (request.Configuration)
                response = await localMember.AppendEntriesAsync(request.Id, request.Term, request.Entries, request.PrevLogIndex, request.PrevLogTerm, request.CommitIndex, request.Configuration, request.ApplyConfig, token).ConfigureAwait(false);

            // skip remaining log entries
            while (await request.Entries.MoveNextAsync().ConfigureAwait(false))
                await protocol.SkipAsync(token).ConfigureAwait(false);
        }

        protocol.Reset();
        await protocol.WriteResponseAsync(in response, token).ConfigureAwait(false);
    }

    public new ValueTask DisposeAsync() => base.DisposeAsync();
}