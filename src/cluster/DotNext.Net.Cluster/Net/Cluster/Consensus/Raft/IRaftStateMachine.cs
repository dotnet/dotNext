using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft;

internal interface IRaftStateMachine
{
    ILogger Logger { get; }

    IReadOnlyCollection<IRaftClusterMember> Members { get; }

    void UpdateLeaderStickiness();

    internal interface IWeakCallerStateIdentity
    {
        bool IsValid(object? state);

        void Clear();
    }

    ref readonly TagList MeasurementTags { get; }
}

internal interface IRaftStateMachine<TMember> : IRaftStateMachine
    where TMember : class, IRaftClusterMember
{
    new IReadOnlyCollection<TMember> Members { get; }

    IReadOnlyCollection<IRaftClusterMember> IRaftStateMachine.Members => Members;

    void MoveToFollowerState(IWeakCallerStateIdentity callerState, bool randomizeTimeout, long? newTerm);

    void MoveToCandidateState(IWeakCallerStateIdentity callerState);

    void MoveToLeaderState(IWeakCallerStateIdentity callerState, TMember leader);

    void UnavailableMemberDetected(IWeakCallerStateIdentity callerState, TMember member, CancellationToken token);
}