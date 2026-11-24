using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

/// <summary>
/// Represents leadership state.
/// </summary>
/// <param name="Term">The term of the leadership.</param>
/// <param name="Stamp">
/// The stamp of the heartbeat rounds performed by the leader with <paramref name="Term"/> value.
/// The stamp is incremented twice per round: at the start (odd value means that the round is in progress)
/// and at the completion confirmed by the majority (even value).
/// </param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct LeadershipTicket(long Term, long Stamp);
