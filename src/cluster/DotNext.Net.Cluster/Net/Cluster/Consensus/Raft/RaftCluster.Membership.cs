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
    }

    private IMemberList members;
    private InvocationList<Action<RaftCluster<TMember>, RaftClusterMemberEventArgs<TMember>>> memberAddedHandlers, memberRemovedHandlers;
    private Atomic.Boolean membershipState;

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
    /// <returns><see langword="true"/> if the member is added successfully; <see langword="false"/> if the member is already in the list.</returns>
    public async ValueTask<bool> AddMemberAsync(TMember member, CancellationToken token)
    {
        var tokenHolder = CombineTokens([token, LifecycleToken]);
        var lockTaken = false;
        try
        {
            await transitionLock.AcquireAsync(tokenHolder.Token).ConfigureAwait(false);
            lockTaken = true;

            // assuming that the member is in sync with the leader
            member.State.NextIndex = auditTrail.LastEntryIndex + 1L;

            if (!members.TryAdd(member, out members))
                return false;

            // synchronize with reader thread
            Interlocked.MemoryBarrierProcessWide();
        }
        catch (OperationCanceledException e) when (tokenHolder.Token == e.CancellationToken)
        {
            throw new OperationCanceledException(e.Message, e, tokenHolder.CancellationOrigin);
        }
        finally
        {
            if (lockTaken)
                transitionLock.Release();

            await tokenHolder.DisposeAsync().ConfigureAwait(false);
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
        var tokenHolder = CombineTokens([token, LifecycleToken]);
        var lockTaken = false;
        try
        {
            await transitionLock.AcquireAsync(tokenHolder.Token).ConfigureAwait(false);
            lockTaken = true;

            if ((result = members.TryRemove(id, out members)) is not null)
            {
                // synchronize with reader thread
                Interlocked.MemoryBarrierProcessWide();

                if (result is { IsRemote: false } && state is not StandbyState<TMember>)
                {
                    // local member is removed, downgrade it
                    await MoveToStandbyState(resumable: false).ConfigureAwait(false);
                }

                if (ReferenceEquals(result, Leader))
                    Leader = null;
            }
        }
        catch (OperationCanceledException e) when (tokenHolder.Token == e.CancellationToken)
        {
            throw new OperationCanceledException(e.Message, e, tokenHolder.CancellationOrigin);
        }
        finally
        {
            if (lockTaken)
                transitionLock.Release();

            await tokenHolder.DisposeAsync().ConfigureAwait(false);
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
    /// <exception cref="NotLeaderException">The current node is not a leader.</exception>
    /// <exception cref="ConcurrentMembershipModificationException">The method is called concurrently.</exception>
    protected async Task<bool> AddMemberAsync<TAddress>(TMember member, int rounds, IClusterConfigurationStorage<TAddress> configurationStorage, Func<TMember, TAddress> addressProvider, CancellationToken token = default)
        where TAddress : notnull
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rounds);

        if (!membershipState.FalseToTrue())
            throw new ConcurrentMembershipModificationException();

        var leaderState = LeaderStateOrException;
        var tokenSource = CombineTokens([token, leaderState.Token]);
        try
        {
            // catch up node
            member.State.NextIndex = auditTrail.LastEntryIndex + 1;
            long currentIndex;
            do
            {
                var commitIndex = auditTrail.LastCommittedEntryIndex;
                currentIndex = auditTrail.LastEntryIndex;
                var precedingIndex = member.State.PrecedingIndex;
                var precedingTerm = await auditTrail.GetTermAsync(precedingIndex, tokenSource.Token).ConfigureAwait(false);
                var term = Term;

                // do replication
                var result = await CatchUpAsync(member, commitIndex, term, precedingIndex, precedingTerm, currentIndex, tokenSource.Token).ConfigureAwait(false);

                if (!result.Value && result.Term > term)
                    return false;
            } while (--rounds > 0 && currentIndex >= member.State.NextIndex);

            // ensure that previous configuration has been committed
            await configurationStorage.WaitForApplyAsync(tokenSource.Token).ConfigureAwait(false);

            // proposes a new member
            if (await configurationStorage.AddMemberAsync(addressProvider(member), tokenSource.Token).ConfigureAwait(false))
            {
                await ReplicateAsync(leaderState, tokenSource.Token).ConfigureAwait(false);

                // ensure that the newly added member has been committed
                await configurationStorage.WaitForApplyAsync(tokenSource.Token).ConfigureAwait(false);
                return true;
            }
        }
        catch (OperationCanceledException e) when (e.CausedBy(tokenSource, leaderState.Token))
        {
            throw new NotLeaderException(e);
        }
        catch (OperationCanceledException e) when (e.CancellationToken == tokenSource.Token)
        {
            throw new OperationCanceledException(e.Message, e, tokenSource.CancellationOrigin);
        }
        finally
        {
            await tokenSource.DisposeAsync().ConfigureAwait(false);
            membershipState.Value = false;
        }

        return false;
    }

    private ValueTask<Result<bool>> CatchUpAsync(TMember member, long commitIndex, long term, long precedingIndex, long precedingTerm, long currentIndex, CancellationToken token)
    {
        var replicator = new LeaderState<TMember>.Replicator(member, Logger);
        replicator.Initialize(ConfigurationStorage.ActiveConfiguration, ConfigurationStorage.ProposedConfiguration, commitIndex, term, precedingIndex, precedingTerm);
        return replicator.ReplicateAsync(auditTrail, currentIndex, token);
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
    /// <exception cref="NotLeaderException">The current node is not a leader.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled or the cluster elects a new leader.</exception>
    /// <exception cref="ConcurrentMembershipModificationException">The method is called concurrently.</exception>
    protected async Task<bool> RemoveMemberAsync<TAddress>(ClusterMemberId id, IClusterConfigurationStorage<TAddress> configurationStorage, Func<TMember, TAddress> addressProvider, CancellationToken token = default)
        where TAddress : notnull
    {
        if (!membershipState.FalseToTrue())
            throw new ConcurrentMembershipModificationException();

        if (members.TryGetValue(id, out var member))
        {
            var leaderState = LeaderStateOrException;
            var tokenSource = CombineTokens([token, leaderState.Token]);
            try
            {
                // ensure that previous configuration has been committed
                await configurationStorage.WaitForApplyAsync(tokenSource.Token).ConfigureAwait(false);

                // remove the existing member
                if (await configurationStorage.RemoveMemberAsync(addressProvider(member), tokenSource.Token).ConfigureAwait(false))
                {
                    await ReplicateAsync(leaderState, tokenSource.Token).ConfigureAwait(false);

                    // ensure that the removed member has been committed
                    await configurationStorage.WaitForApplyAsync(tokenSource.Token).ConfigureAwait(false);
                    return true;
                }
            }
            catch (OperationCanceledException e) when (e.CausedBy(tokenSource, leaderState.Token))
            {
                throw new NotLeaderException(e);
            }
            catch (OperationCanceledException e) when (e.CancellationToken == tokenSource.Token)
            {
                throw new OperationCanceledException(e.Message, e, tokenSource.CancellationOrigin);
            }
            finally
            {
                await tokenSource.DisposeAsync().ConfigureAwait(false);
                membershipState.Value = false;
            }
        }

        return false;
    }
}