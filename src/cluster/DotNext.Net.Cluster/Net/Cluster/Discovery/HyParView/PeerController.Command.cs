using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Discovery.HyParView;

using IRumorSender = Messaging.Gossip.IRumorSender;

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
        internal CommandType Type { get; private init; } // TODO: Change to required in C# 11

        [DisallowNull]
        private EndPoint? Address1 { get; init; }

        [DisallowNull]
        private EndPoint? Address2 { get; init; }

        private int Int32Param { get; init; }

        [DisallowNull]
        private object? ObjectParam { get; init; }

        internal static Command Join(EndPoint joinedPeer) => new() { Type = CommandType.Join, Address1 = joinedPeer };

        internal void Join(out EndPoint joinedPeer)
        {
            Debug.Assert(Type is CommandType.Join);
            Debug.Assert(Address1 is not null);

            joinedPeer = Address1;
        }

        internal static Command ForwardJoin(EndPoint sender, EndPoint joinedPeer, int ttl)
            => new() { Type = CommandType.ForwardJoin, Address1 = sender, Address2 = joinedPeer, Int32Param = ttl };

        internal void ForwardJoin(out EndPoint sender, out EndPoint joinedPeer, out int ttl)
        {
            Debug.Assert(Type is CommandType.ForwardJoin);
            Debug.Assert(Address1 is not null);
            Debug.Assert(Address2 is not null);

            sender = Address1;
            joinedPeer = Address2;
            ttl = Int32Param;
        }

        internal static Command Neighbor(EndPoint sender, bool highPriority)
            => new() { Type = CommandType.Neighbor, Address1 = sender, Int32Param = Unsafe.BitCast<bool, byte>(highPriority) };

        internal void Neighbor(out EndPoint sender, out bool highPriority)
        {
            Debug.Assert(Type is CommandType.Neighbor);
            Debug.Assert(Address1 is not null);

            sender = Address1;
            highPriority = Unsafe.BitCast<byte, bool>((byte)Int32Param);
        }

        internal static Command Disconnect(EndPoint sender, bool isAlive)
            => new() { Type = CommandType.Disconnect, Address1 = sender, Int32Param = Unsafe.BitCast<bool, byte>(isAlive) };

        internal void Disconnect(out EndPoint sender, out bool isAlive)
        {
            Debug.Assert(Type is CommandType.Disconnect);
            Debug.Assert(Address1 is not null);

            sender = Address1;
            isAlive = Unsafe.BitCast<byte, bool>((byte)Int32Param);
        }

        internal static Command Shuffle(EndPoint sender, EndPoint origin, IReadOnlyCollection<EndPoint> peers, int ttl)
            => new() { Type = CommandType.Shuffle, Address1 = sender, Address2 = origin, ObjectParam = peers, Int32Param = ttl };

        internal void Shuffle(out EndPoint sender, out EndPoint origin, out IReadOnlyCollection<EndPoint> peers, out int ttl)
        {
            Debug.Assert(Type is CommandType.Shuffle);
            Debug.Assert(Address1 is not null);
            Debug.Assert(Address2 is not null);
            Debug.Assert(ObjectParam is IReadOnlyCollection<EndPoint>);

            sender = Address1;
            origin = Address2;
            peers = (IReadOnlyCollection<EndPoint>)ObjectParam;
            ttl = Int32Param;
        }

        internal static Command ForceShuffle() => new() { Type = CommandType.ForceShuffle };

        internal static Command ShuffleReply(IReadOnlyCollection<EndPoint> peers)
            => new() { Type = CommandType.ShuffleReply, ObjectParam = peers };

        internal void ShuffleReply(out IReadOnlyCollection<EndPoint> peers)
        {
            Debug.Assert(Type is CommandType.ShuffleReply);
            Debug.Assert(ObjectParam is IReadOnlyCollection<EndPoint>);

            peers = (IReadOnlyCollection<EndPoint>)ObjectParam;
        }

        internal static Command Broadcast(Func<PeerController, IRumorSender> senderFactory)
            => new() { Type = CommandType.Broadcast, ObjectParam = senderFactory };

        internal void Broadcast(out Func<PeerController, IRumorSender> senderFactory)
        {
            Debug.Assert(Type is CommandType.Broadcast);
            Debug.Assert(ObjectParam is Func<PeerController, IRumorSender>);

            senderFactory = (Func<PeerController, IRumorSender>)ObjectParam;
        }
    }
}