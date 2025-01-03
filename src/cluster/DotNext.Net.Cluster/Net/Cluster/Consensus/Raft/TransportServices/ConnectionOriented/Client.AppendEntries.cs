using System.Runtime.CompilerServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using Buffers;
using IO;
using static Collections.Specialized.ConcurrentTypeMapExtensions;
using IClusterConfiguration = Membership.IClusterConfiguration;

internal partial class Client : RaftClusterMember
{
    // optimized version for empty heartbeats (it has no field to store empty entries)
    private class AppendEntriesExchange : IClientExchange<Result<HeartbeatResult>>, IResettable
    {
        private const string Name = "AppendEntries";

        private long term, prevLogIndex, prevLogTerm, commitIndex;
        protected IClusterConfiguration? config;
        private bool applyConfig;

        internal void Initialize(long term, long prevLogIndex, long prevLogTerm, long commitIndex, IClusterConfiguration config, bool applyConfig)
        {
            Debug.Assert(config is not null);

            this.term = term;
            this.prevLogIndex = prevLogIndex;
            this.prevLogTerm = prevLogTerm;
            this.commitIndex = commitIndex;
            this.config = config;
            this.applyConfig = applyConfig;
        }

        public virtual void Reset()
        {
            config = null;
        }

        ValueTask IClientExchange<Result<HeartbeatResult>>.RequestAsync(ILocalMember localMember, ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
        {
            Debug.Assert(config is not null);

            // write header
            protocol.AdvanceWriteCursor(WriteHeaders(protocol, in localMember.Id, entriesCount: 0));

            // write config
            if (config.Length > 0L)
                return WriteConfigurationToTransportAsync(protocol, buffer, token);

            return protocol.WriteToTransportAsync(token);
        }

        private async ValueTask WriteConfigurationToTransportAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
        {
            Debug.Assert(config is not null);

            protocol.StartFrameWrite();
            await config.WriteToAsync(protocol, buffer, token).ConfigureAwait(false);
            protocol.WriteFinalFrame();
            await protocol.WriteToTransportAsync(token).ConfigureAwait(false);
        }

        protected int WriteHeaders(ProtocolStream protocol, in ClusterMemberId sender, int entriesCount)
        {
            Debug.Assert(config is not null);

            var writer = protocol.BeginRequestMessage(MessageType.AppendEntries);
            writer.Write<AppendEntriesMessage>(new(sender, term, prevLogIndex, prevLogTerm, commitIndex, entriesCount));
            writer.Add(Unsafe.BitCast<bool, byte>(applyConfig));
            writer.Write<ConfigurationMessage>(new(config));
            return writer.WrittenCount;
        }

        static ValueTask<Result<HeartbeatResult>> IClientExchange<Result<HeartbeatResult>>.ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
            => protocol.ReadHeartbeatResultAsync(token);

        static string IClientExchange<Result<HeartbeatResult>>.Name => Name;
    }

    private sealed class AppendEntriesExchange<TEntry, TList> : AppendEntriesExchange, IClientExchange<Result<HeartbeatResult>>
        where TEntry : IRaftLogEntry
        where TList : IReadOnlyList<TEntry>
    {
        private TList? entries;

        internal void Initialize(long term, TList entries, long prevLogIndex, long prevLogTerm, long commitIndex, IClusterConfiguration config, bool applyConfig)
        {
            this.entries = entries;
            Initialize(term, prevLogIndex, prevLogTerm, commitIndex, config, applyConfig);
        }

        public override void Reset()
        {
            entries = default;
            base.Reset();
        }

        async ValueTask IClientExchange<Result<HeartbeatResult>>.RequestAsync(ILocalMember localMember, ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
        {
            Debug.Assert(config is not null);
            Debug.Assert(entries is not null);

            // write header
            protocol.AdvanceWriteCursor(WriteHeaders(protocol, in localMember.Id, entries.Count));

            // write config
            if (config.Length > 0L)
            {
                protocol.StartFrameWrite();
                await config.WriteToAsync(protocol, buffer, token).ConfigureAwait(false);
                protocol.WriteFinalFrame();
            }

            // write log entries (do not use GetEnumerator() to avoid allocations)
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];

                // remaining buffer should have free space enough for placing a frame with
                // log entry metadata and at least 1 byte for the payload
                if (protocol.CanWriteFrameSynchronously(LogEntryMetadata.Size + 1) is false)
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

    private protected sealed override Task<Result<HeartbeatResult>> AppendEntriesAsync<TEntry, TList>(long term, TList entries, long prevLogIndex, long prevLogTerm, long commitIndex, IClusterConfiguration config, bool applyConfig, CancellationToken token)
    {
        if (entries.Count is 0)
        {
            var exchange = exchangeCache.RemoveOrCreate<AppendEntriesExchange>();
            exchange.Initialize(term, prevLogIndex, prevLogTerm, commitIndex, config, applyConfig);
            return RequestAsync<Result<HeartbeatResult>, AppendEntriesExchange>(exchange, token);
        }
        else
        {
            var exchange = exchangeCache.RemoveOrCreate<AppendEntriesExchange<TEntry, TList>>();
            exchange.Initialize(term, entries, prevLogIndex, prevLogTerm, commitIndex, config, applyConfig);
            return RequestAsync<Result<HeartbeatResult>, AppendEntriesExchange<TEntry, TList>>(exchange, token);
        }
    }
}