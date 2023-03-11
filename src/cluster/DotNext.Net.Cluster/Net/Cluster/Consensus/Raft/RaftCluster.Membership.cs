using System.Runtime.Serialization;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Collections.Specialized;
using Membership;
using Threading;

public partial class RaftCluster<TMember>
{
    private interface IMemberList : IReadOnlyDictionary<ClusterMemberId, TMember>
    {
        new IReadOnlyCollection<TMember> Values { get; }

        bool TryAdd(TMember member, out IMemberList list);

        TMember? TryRemove(ClusterMemberId id, out IMemberList list);

        internal static IMemberList Empty { get; } = new MemberList();
    }

    private sealed class MemberList : Dictionary<ClusterMemberId, TMember>, IMemberList
    {
        internal MemberList()
            : base(10)
        {
        }

        private MemberList(MemberList origin)
            : base(origin)
        {
        }

        IReadOnlyCollection<TMember> IMemberList.Values => Values;

        bool IMemberList.TryAdd(TMember member, out IMemberList list)
        {
            MemberList tmp;

            // O(n) complexity, but it's fine since the number of nodes is relatively small (not even hundreds)
            if (!ContainsKey(member.Id) && (tmp = new(this)).TryAdd(member.Id, member))
            {
                list = tmp;
                return true;
            }

            list = this;
            return false;
        }

        TMember? IMemberList.TryRemove(ClusterMemberId id, out IMemberList list)
        {
            MemberList tmp;

            // O(n) complexity, but it's fine since the number of nodes is relatively small (not even hundreds)
            if (ContainsKey(id) && (tmp = new(this)).Remove(id, out var result))
            {
                list = tmp;
            }
            else
            {
                result = null;
                list = this;
            }

            return result;
        }
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

    private IMemberList members;
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
    /// <remarks>
    /// This method is exposed to be called by <see cref="IClusterConfigurationStorage{TAddress}.ActiveConfigurationChanged"/>
    /// handler.
    /// </remarks>
    /// <param name="member">The member to add.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if the member is addedd successfully; <see langword="false"/> if the member is already in the list.</returns>
    public async ValueTask<bool> AddMemberAsync(TMember member, CancellationToken token)
    {
        var tokenHolder = token.LinkTo(LifecycleToken);
        var lockHolder = default(AsyncLock.Holder);
        try
        {
            lockHolder = await transitionSync.AcquireAsync(token).ConfigureAwait(false);

            // assuming that the member is in sync with the leader
            member.NextIndex = auditTrail.LastUncommittedEntryIndex + 1;

            if (!members.TryAdd(member, out members))
                return false;

            // synchronize with reader thread
            Interlocked.MemoryBarrierProcessWide();
        }
        catch (OperationCanceledException e) when (tokenHolder is not null)
        {
            throw new OperationCanceledException(e.Message, e, tokenHolder.CancellationOrigin);
        }
        finally
        {
            tokenHolder?.Dispose();
            lockHolder.Dispose();
        }

        OnMemberAdded(member);
        return true;
    }

    /// <summary>
    /// Removes the member from the collection of members visible by the current node.
    /// </summary>
    /// <remarks>
    /// This method is exposed to be called by <see cref="IClusterConfigurationStorage{TAddress}.ActiveConfigurationChanged"/>
    /// handler.
    /// </remarks>
    /// <param name="id">The identifier of the member.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The removed member.</returns>
    public async ValueTask<TMember?> RemoveMemberAsync(ClusterMemberId id, CancellationToken token)
    {
        TMember? result;
        var tokenHolder = token.LinkTo(LifecycleToken);
        var lockHolder = default(AsyncLock.Holder);
        try
        {
            lockHolder = await transitionSync.AcquireAsync(token).ConfigureAwait(false);
            if ((result = members.TryRemove(id, out members)) is not null)
            {
                // synchronize with reader thread
                Interlocked.MemoryBarrierProcessWide();

                if (result.IsRemote is false && state is not null)
                {
                    // local member is removed, downgrade it
                    await MoveToStandbyState(resumable: false).ConfigureAwait(false);
                }

                if (ReferenceEquals(result, Leader))
                    Leader = null;
            }
        }
        catch (OperationCanceledException e) when (tokenHolder is not null)
        {
            throw new OperationCanceledException(e.Message, e, tokenHolder.CancellationOrigin);
        }
        finally
        {
            tokenHolder?.Dispose();
            lockHolder.Dispose();
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
                var result = await new LeaderState<TMember>.Replicator(ConfigurationStorage.ActiveConfiguration, ConfigurationStorage.ProposedConfiguration, member, commitIndex, term, precedingIndex, precedingTerm, Logger)
                    .ReplicateAsync(auditTrail, currentIndex, token)
                    .ConfigureAwait(false);

                if (!result.Value && result.Term > term)
                    return false;
            }
            while (--rounds > 0 && currentIndex >= member.NextIndex);

            // ensure that previous configuration has been committed
            await configurationStorage.WaitForApplyAsync(token).ConfigureAwait(false);

            // proposes a new member
            if (await configurationStorage.AddMemberAsync(addressProvider(member), token).ConfigureAwait(false))
            {
                while (!await ReplicateAsync(new EmptyLogEntry(Term), token).ConfigureAwait(false));

                // ensure that the newly added member has been committed
                await configurationStorage.WaitForApplyAsync(token).ConfigureAwait(false);
                return true;
            }

            return false;
        }
        catch (OperationCanceledException e) when (tokenSource is not null)
        {
            throw new OperationCanceledException(e.Message, e, tokenSource.CancellationOrigin);
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
    /// <param name="addressProvider">The delegate that allows to get the address of the member.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the node has been removed from the cluster successfully;
    /// <see langword="false"/> if the node rejects the replication or the address of the node cannot be committed.
    /// </returns>
    /// <exception cref="InvalidOperationException">The current node is not a leader.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled or the cluster elects a new leader.</exception>
    /// <exception cref="ConcurrentMembershipModificationException">The method is called concurrently.</exception>
    protected async Task<bool> RemoveMemberAsync<TAddress>(ClusterMemberId id, IClusterConfigurationStorage<TAddress> configurationStorage, Func<TMember, TAddress> addressProvider, CancellationToken token = default)
        where TAddress : notnull
    {
        if (!membershipState.FalseToTrue())
            throw new ConcurrentMembershipModificationException();

        if (members.TryGetValue(id, out var member))
        {
            var tokenSource = token.LinkTo(LeadershipToken);
            try
            {
                // ensure that previous configuration has been committed
                await configurationStorage.WaitForApplyAsync(token).ConfigureAwait(false);

                // remove the existing member
                if (await configurationStorage.RemoveMemberAsync(addressProvider(member), token).ConfigureAwait(false))
                {
                    while (!await ReplicateAsync(new EmptyLogEntry(Term), token).ConfigureAwait(false));

                    // ensure that the removed member has been committed
                    await configurationStorage.WaitForApplyAsync(token).ConfigureAwait(false);
                    return true;
                }
            }
            catch (OperationCanceledException e) when (tokenSource is not null)
            {
                throw new OperationCanceledException(e.Message, e, tokenSource.CancellationOrigin);
            }
            finally
            {
                tokenSource?.Dispose();
                membershipState.Value = false;
            }
        }

        return false;
    }
}