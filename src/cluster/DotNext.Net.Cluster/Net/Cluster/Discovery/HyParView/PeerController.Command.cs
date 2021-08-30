using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
        private readonly struct Command
        {
            private readonly object? peersOrMessageTransport;

            internal CommandType Type { get; init; }

            // null only if Type is ShuffleReply or ForceShuffle
            [DisallowNull]
            internal EndPoint? Sender { get; init; }

            [DisallowNull]
            internal EndPoint? Origin { get; init; }

            internal bool IsAliveOrHighPriority
            {
                get => TimeToLive != 0;
                init => TimeToLive = value.ToInt32();
            }

            internal int TimeToLive { get; init; }

            [DisallowNull]
            internal IRumourSender? RumourTransport
            {
                get => peersOrMessageTransport as IRumourSender;
                init => peersOrMessageTransport = value;
            }

            internal IReadOnlyCollection<EndPoint> Peers
            {
                get => peersOrMessageTransport as IReadOnlyCollection<EndPoint> ?? Array.Empty<EndPoint>();
                init => peersOrMessageTransport = value;
            }

            internal static Command Join(EndPoint joinedPeer) => new() { Type = CommandType.Join, Sender = joinedPeer };

            internal static Command ForwardJoin(EndPoint sender, EndPoint joinedPeer, int ttl) => new() { Type = CommandType.ForwardJoin, Sender = sender, Origin = joinedPeer, TimeToLive = ttl };

            internal static Command Neighbor(EndPoint sender, bool highPriority) => new() { Type = CommandType.Neighbor, Sender = sender, IsAliveOrHighPriority = highPriority };

            internal static Command Disconnect(EndPoint sender, bool isAlive) => new() { Type = CommandType.Disconnect, Sender = sender, IsAliveOrHighPriority = isAlive };

            internal static Command Shuffle(EndPoint sender, EndPoint origin, IReadOnlyCollection<EndPoint> peers, int ttl) => new() { Type = CommandType.Shuffle, Sender = sender, Origin = origin, Peers = peers, TimeToLive = ttl };

            internal static Command ForceShuffle() => new() { Type = CommandType.ForceShuffle };

            internal static Command ShuffleReply(IReadOnlyCollection<EndPoint> peers) => new() { Type = CommandType.ShuffleReply, Peers = peers };

            internal static Command Broadcast(IRumourSender sender) => new() { Type = CommandType.Broadcast, RumourTransport = sender };
        }
    }
}