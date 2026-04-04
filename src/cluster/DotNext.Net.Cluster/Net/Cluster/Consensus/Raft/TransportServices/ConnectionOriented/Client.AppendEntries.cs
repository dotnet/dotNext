using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using Buffers;
using IO;

internal partial class Client
{
    private interface IAppendEntriesExchange : IClientExchange<Result<HeartbeatResult>>
    {
        static string IClientExchange<Result<HeartbeatResult>>.Name => "AppendEntries";
        
        static ValueTask<Result<HeartbeatResult>> IClientExchange<Result<HeartbeatResult>>.ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadHeartbeatResultAsync(token);
    }
    
    // optimized version for empty heartbeats (it has no field to store empty entries)
    [StructLayout(LayoutKind.Auto)]
    private readonly struct AppendEntriesExchange(long term, long prevLogIndex, long prevLogTerm, long commitIndex) : IAppendEntriesExchange
    {
        ValueTask IClientExchange<Result<HeartbeatResult>>.RequestAsync(ILocalMember localMember, ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
        {
            // write header
            protocol.AdvanceWriteCursor(WriteHeaders(protocol, in localMember.Id, entriesCount: 0));
            return protocol.WriteToTransportAsync(token);
        }

        public int WriteHeaders(ProtocolStream protocol, in ClusterMemberId sender, int entriesCount)
        {
            var writer = protocol.BeginRequestMessage(MessageType.AppendEntries);
            writer.Write<AppendEntriesMessage>(new(sender, term, prevLogIndex, prevLogTerm, commitIndex, entriesCount));
            return writer.WrittenCount;
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct AppendEntriesExchange<TEntry, TList>(long term, TList entries, long prevLogIndex, long prevLogTerm, long commitIndex)
        : IAppendEntriesExchange
        where TEntry : IRaftLogEntry
        where TList : IReadOnlyList<TEntry>
    {
        private readonly AppendEntriesExchange exchange = new(term, prevLogIndex, prevLogTerm, commitIndex);

        ValueTask IClientExchange<Result<HeartbeatResult>>.RequestAsync(ILocalMember localMember, ProtocolStream protocol, Memory<byte> buffer,
            CancellationToken token)
        {
            // write header
            protocol.AdvanceWriteCursor(exchange.WriteHeaders(protocol, in localMember.Id, entries.Count));
            return RequestAsync(entries, protocol, buffer, token);
        }

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        private static async ValueTask RequestAsync(TList entries, ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
        {
            // write log entries (do not use GetEnumerator() to avoid allocations)
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                // remaining buffer should have free space enough for placing a frame with
                // log entry metadata and at least 1 byte for the payload
                if (!protocol.CanWriteFrameSynchronously(LogEntryMetadata.Size + 1))
                    await protocol.WriteToTransportAsync(token).ConfigureAwait(false);

                LogEntryMetadata.Create(entry).Format(protocol.RemainingBufferSpan);
                protocol.AdvanceWriteCursor(LogEntryMetadata.Size);

                protocol.StartFrameWrite();
                await entry.WriteToAsync(protocol, buffer, token).ConfigureAwait(false);
                protocol.WriteFinalFrame();
            }

            await protocol.WriteToTransportAsync(token).ConfigureAwait(false);
        }
    }

    private protected sealed override Task<Result<HeartbeatResult>> AppendEntriesAsync<TEntry, TList>(long term, TList entries, long prevLogIndex,
        long prevLogTerm, long commitIndex, CancellationToken token)
        => entries.Count is 0
            ? RequestAsync<Result<HeartbeatResult>, AppendEntriesExchange>(new(term, prevLogIndex, prevLogTerm, commitIndex), token)
            : RequestAsync<Result<HeartbeatResult>, AppendEntriesExchange<TEntry, TList>>(new(term, entries, prevLogIndex, prevLogTerm, commitIndex),
                token);
}