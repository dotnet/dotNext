using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Discovery.HyParView
{
    using IRumourSender = Messaging.Gossip.IRumourSender;

    public partial class PeerController
    {
        private enum CommandType
        {
            Unknown = 0,

            Join,

            ForwardJoin,

            Disconnect,

            Neighbor,

            Shuffle,

            ShuffleReply,

            ForceShuffle,

            Broadcast,
        }

        // we use this struct as a placeholder for all HyParView commands to reduce GC pressure
        [StructLayout(LayoutKind.Auto)]
        private readonly struct Command // TODO: Migrate to init-only properties
        {
            private readonly object? peersOrMessageTransport;
            private readonly int ttlOrHighPriority;

            private Command(CommandType type, EndPoint? sender, EndPoint? origin, IReadOnlyCollection<EndPoint>? peers, int ttlOrHighPriority)
            {
                Type = type;
                Sender = sender;
                Origin = origin;
                peersOrMessageTransport = peers;
                this.ttlOrHighPriority = ttlOrHighPriority;
            }

            private Command(IRumourSender sender)
            {
                Type = CommandType.Broadcast;
                Sender = null;
                Origin = null;
                peersOrMessageTransport = sender;
                ttlOrHighPriority = default;
            }

            internal CommandType Type { get; }

            // null only if Type is ShuffleReply or ForceShuffle
            internal EndPoint? Sender { get; }

            internal EndPoint? Origin { get; }

            internal bool IsAliveOrHighPriority => ttlOrHighPriority != 0;

            internal int TimeToLive => ttlOrHighPriority;

            internal IRumourSender? RumourTransport => peersOrMessageTransport as IRumourSender;

            internal IReadOnlyCollection<EndPoint> Peers => peersOrMessageTransport as IReadOnlyCollection<EndPoint> ?? Array.Empty<EndPoint>();

            internal static Command Join(EndPoint joinedPeer) => new(CommandType.Join, joinedPeer, null, null, default);

            internal static Command ForwardJoin(EndPoint sender, EndPoint joinedPeer, int ttl) => new(CommandType.ForwardJoin, sender, joinedPeer, null, ttl);

            internal static Command Neighbor(EndPoint sender, bool highPriority) => new(CommandType.Neighbor, sender, null, null, highPriority.ToInt32());

            internal static Command Disconnect(EndPoint sender, bool isAlive) => new(CommandType.Disconnect, sender, null, null, isAlive.ToInt32());

            internal static Command Shuffle(EndPoint sender, EndPoint origin, IReadOnlyCollection<EndPoint> peers, int ttl) => new(CommandType.Shuffle, sender, origin, peers, ttl);

            internal static Command ForceShuffle() => new(CommandType.ForceShuffle, null, null, null, default);

            internal static Command ShuffleReply(IReadOnlyCollection<EndPoint> peers) => new(CommandType.ShuffleReply, null, null, peers, default);

            internal static Command Broadcast(IRumourSender sender) => new(sender);
        }
    }
}