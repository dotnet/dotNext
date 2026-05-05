using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Diagnostics;

internal interface IRaftStateMachine
{
    ILogger Logger { get; }

    IReadOnlyCollection<IRaftClusterMember> Members { get; }

    void UpdateLeaderStickiness(Timestamp refreshedAt);

    internal interface IWeakCallerStateIdentity
    {
        bool IsValid([NotNullWhen(true)] object? state);

        void Clear();
    }

    ref readonly TagList MeasurementTags { get; }
}

internal interface IRaftStateMachine<TMember> : IRaftStateMachine
    where TMember : class, IRaftClusterMember
{
    new IReadOnlyCollection<TMember> Members { get; }

    IReadOnlyCollection<IRaftClusterMember> IRaftStateMachine.Members => Members;

    Task MoveToFollowerState(IWeakCallerStateIdentity callerState, bool randomizeTimeout, long? newTerm);

    Task MoveToCandidateState(IWeakCallerStateIdentity callerState);

    Task MoveToLeaderState(IWeakCallerStateIdentity callerState, TMember leader);

    Task UnavailableMemberDetected(IWeakCallerStateIdentity callerState, TMember member, CancellationToken token);

    Task IncomingHeartbeatTimedOut(IWeakCallerStateIdentity callerState);
}