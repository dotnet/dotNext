using System.Collections;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Collections.Specialized;
using Membership;
using Threading;

public partial class RaftCluster<TMember>
{
    internal sealed class MemberList : IReadOnlyDictionary<ClusterMemberId, TMember>, IReadOnlyCollection<TMember>
    {
        internal static readonly MemberList Empty = new();

        private readonly ImmutableDictionary<ClusterMemberId, TMember> dictionary;

        internal MemberList(IReadOnlyDictionary<ClusterMemberId, TMember> members)
            => dictionary = ImmutableDictionary.CreateRange(members);

        private MemberList(ImmutableDictionary<ClusterMemberId, TMember> dictionary)
            => this.dictionary = dictionary;

        private MemberList()
            : this(ImmutableDictionary<ClusterMemberId, TMember>.Empty)
        {
        }

        public TMember this[ClusterMemberId id] => dictionary[id];

        public int Count => dictionary.Count;

        public bool ContainsKey(ClusterMemberId id) => dictionary.ContainsKey(id);

        public IEnumerable<ClusterMemberId> Keys => dictionary.Keys;

        public IEnumerable<TMember> Values => dictionary.Values;

        internal static bool TryAdd(ref MemberList membership, TMember member)
        {
            var dictionary = membership.dictionary;
            var result = ImmutableInterlocked.TryAdd(ref dictionary, member.Id, member);
            if (!ReferenceEquals(membership.dictionary, dictionary))
                membership = new(dictionary);

            return result;
        }

        internal static bool TryRemove(ref MemberList membership, ClusterMemberId id, [NotNullWhen(true)] out TMember? member)
        {
            var dictionary = membership.dictionary;
            var result = ImmutableInterlocked.TryRemove(ref dictionary, id, out member);
            if (!ReferenceEquals(membership.dictionary, dictionary))
                membership = new(dictionary);

            return result;
        }

        public bool TryGetValue(ClusterMemberId id, [MaybeNullWhen(false)] out TMember member)
            => dictionary.TryGetValue(id, out member);

        internal MemberList Clear() => new(dictionary.Clear());

        IEnumerator<TMember> IEnumerable<TMember>.GetEnumerator() => Values.GetEnumerator();

        IEnumerator<KeyValuePair<ClusterMemberId, TMember>> IEnumerable<KeyValuePair<ClusterMemberId, TMember>>.GetEnumerator() => dictionary.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => Values.GetEnumerator();
    }

    /// <summary>
    /// Indicates that the caller is trying to add or remove cluster member concurrently.
    /// </summary>
    /// <remarks>
    /// The current implementation of Raft doesn't support adding or removing multiple cluster members at a time.
    /// </remarks>
    [Serializable]
    public sealed class ConcurrentMembershipModificationException : RaftProtocolException
    {
        internal ConcurrentMembershipModificationException()
            : base(ExceptionMessages.ConcurrentMembershipUpdate)
        {
        }

        private ConcurrentMembershipModificationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    private MemberList members;
    private InvocationList<Action<RaftCluster<TMember>, RaftClusterMemberEventArgs<TMember>>> memberAddedHandlers, memberRemovedHandlers;
    private AtomicBoolean membershipState;

    /// <summary>
    /// Gets the member by its identifier.
    /// </summary>
    /// <param name="id">The identifier of the cluster member.</param>
    /// <returns><see langword="true"/> if member found; otherwise, <see langword="false"/>.</returns>
    protected TMember? TryGetMember(ClusterMemberId id)
        => members.TryGetValue(id, out var result) ? result : null;

    /// <summary>
    /// An event raised when new cluster member is detected.
    /// </summary>
    public event Action<RaftCluster<TMember>, RaftClusterMemberEventArgs<TMember>> MemberAdded
    {
        add => memberAddedHandlers += value;
        remove => memberRemovedHandlers -= value;
    }

    /// <inheritdoc />
    event Action<IPeerMesh, PeerEventArgs> IPeerMesh.PeerDiscovered
    {
        add => memberAddedHandlers += value;
        remove => memberAddedHandlers -= value;
    }

    private void OnMemberAdded(TMember member)
    {
        if (!memberAddedHandlers.IsEmpty)
            memberAddedHandlers.Invoke(this, new(member));
    }

    /// <summary>
    /// An event raised when cluster member is removed gracefully.
    /// </summary>
    public event Action<RaftCluster<TMember>, RaftClusterMemberEventArgs<TMember>> MemberRemoved
    {
        add => memberRemovedHandlers += value;
        remove => memberRemovedHandlers -= value;
    }

    /// <inheritdoc />
    event Action<IPeerMesh, PeerEventArgs> IPeerMesh.PeerGone
    {
        add => memberRemovedHandlers += value;
        remove => memberRemovedHandlers -= value;
    }

    private void OnMemberRemoved(TMember member)
    {
        if (!memberRemovedHandlers.IsEmpty)
            memberRemovedHandlers.Invoke(this, new(member));
    }

    /// <summary>
    /// Adds a new member to the collection of members visible by the current node.
    /// </summary>
    /// <param name="member">The member to add.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if the member is addedd successfully; <see langword="false"/> if the member is already in the list.</returns>
    protected async ValueTask<bool> AddMemberAsync(TMember member, CancellationToken token)
    {
        using var tokenHolder = token.LinkTo(LifecycleToken);

        using (await transitionSync.AcquireAsync(token).ConfigureAwait(false))
        {
            // assuming that the member is in sync with the leader
            member.NextIndex = auditTrail.LastUncommittedEntryIndex + 1;

            if (!MemberList.TryAdd(ref members, member))
                return false;
        }

        OnMemberAdded(member);
        return true;
    }

    /// <summary>
    /// Removes the member from the collection of members visible by the current node.
    /// </summary>
    /// <param name="id">The identifier of the member.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The removed member.</returns>
    protected async ValueTask<TMember?> RemoveMemberAsync(ClusterMemberId id, CancellationToken token)
    {
        TMember? result;
        using var tokenHolder = token.LinkTo(LifecycleToken);

        using (await transitionSync.AcquireAsync(token).ConfigureAwait(false))
        {
            if (MemberList.TryRemove(ref members, id, out result) && !result.IsRemote && state is not null)
            {
                // local member is removed, downgrade it
                var newState = new StandbyState(this);
                using var currentState = state;
                state = newState;
            }

            if (ReferenceEquals(result, leader))
                Leader = null;
        }

        if (result is not null)
            OnMemberRemoved(result);

        return result;
    }

    /// <summary>
    /// Announces a new member in the cluster.
    /// </summary>
    /// <typeparam name="TAddress">The type of the member address.</typeparam>
    /// <param name="member">The cluster member client used to catch up its state.</param>
    /// <param name="rounds">The number of warmup rounds.</param>
    /// <param name="configurationStorage">The configuration storage.</param>
    /// <param name="addressProvider">The delegate that allows to get the address of the member.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the node has been added to the cluster successfully;
    /// <see langword="false"/> if the node rejects the replication or the address of the node cannot be committed.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rounds"/> is less than or equal to zero.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled or the cluster elects a new leader.</exception>
    /// <exception cref="InvalidOperationException">The current node is not a leader.</exception>
    /// <exception cref="ConcurrentMembershipModificationException">The method is called concurrently.</exception>
    protected async Task<bool> AddMemberAsync<TAddress>(TMember member, int rounds, IClusterConfigurationStorage<TAddress> configurationStorage, Func<TMember, TAddress> addressProvider, CancellationToken token = default)
        where TAddress : notnull
    {
        if (rounds <= 0)
            throw new ArgumentOutOfRangeException(nameof(rounds));

        if (!membershipState.FalseToTrue())
            throw new ConcurrentMembershipModificationException();

        var tokenSource = token.LinkTo(LeadershipToken);
        try
        {
            // catch up node
            member.NextIndex = auditTrail.LastUncommittedEntryIndex + 1;
            long currentIndex;
            do
            {
                var commitIndex = auditTrail.LastCommittedEntryIndex;
                currentIndex = auditTrail.LastUncommittedEntryIndex;
                var precedingIndex = Math.Max(0, member.NextIndex - 1);
                var precedingTerm = await auditTrail.GetTermAsync(precedingIndex, token).ConfigureAwait(false);
                var term = Term;

                // do replication
                var result = await new LeaderState.Replicator(auditTrail, ConfigurationStorage.ActiveConfiguration, ConfigurationStorage.ProposedConfiguration, member, commitIndex, currentIndex, term, precedingIndex, precedingTerm, Logger, token).ReplicateAsync(false).ConfigureAwait(false);

                if (!result.Value && result.Term > term)
                    return false;
            }
            while (rounds > 0 && currentIndex >= member.NextIndex);

            // ensure that previous configuration has been committed
            await configurationStorage.WaitForApplyAsync(token).ConfigureAwait(false);

            // proposes a new member
            if (await configurationStorage.AddMemberAsync(member.Id, addressProvider(member), token).ConfigureAwait(false))
            {
                while (!await ReplicateAsync(new EmptyLogEntry(Term), token).ConfigureAwait(false));

                // ensure that the newly added member has been committed
                await configurationStorage.WaitForApplyAsync(token).ConfigureAwait(false);
                return true;
            }

            return false;
        }
        finally
        {
            tokenSource?.Dispose();
            membershipState.Value = false;
        }
    }

    /// <summary>
    /// Removes the member from the cluster.
    /// </summary>
    /// <typeparam name="TAddress">The type of the member address.</typeparam>
    /// <param name="id">The cluster member to remove.</param>
    /// <param name="configurationStorage">The configuration storage.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the node has been removed from the cluster successfully;
    /// <see langword="false"/> if the node rejects the replication or the address of the node cannot be committed.
    /// </returns>
    /// <exception cref="InvalidOperationException">The current node is not a leader.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled or the cluster elects a new leader.</exception>
    /// <exception cref="ConcurrentMembershipModificationException">The method is called concurrently.</exception>
    public async Task<bool> RemoveMemberAsync<TAddress>(ClusterMemberId id, IClusterConfigurationStorage<TAddress> configurationStorage, CancellationToken token = default)
        where TAddress : notnull
    {
        if (!membershipState.FalseToTrue())
            throw new ConcurrentMembershipModificationException();

        var tokenSource = token.LinkTo(LeadershipToken);
        try
        {
            // ensure that previous configuration has been committed
            await configurationStorage.WaitForApplyAsync(token).ConfigureAwait(false);

            // remove the existing member
            if (await configurationStorage.RemoveMemberAsync(id, token).ConfigureAwait(false))
            {
                while (!await ReplicateAsync(new EmptyLogEntry(Term), token).ConfigureAwait(false));

                // ensure that the removed member has been committed
                await configurationStorage.WaitForApplyAsync(token).ConfigureAwait(false);
                return true;
            }

            return false;
        }
        finally
        {
            tokenSource?.Dispose();
            membershipState.Value = false;
        }
    }
}