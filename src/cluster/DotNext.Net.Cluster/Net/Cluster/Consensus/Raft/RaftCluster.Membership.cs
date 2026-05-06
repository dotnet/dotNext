using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Collections.Specialized;
using ReplicationUtils;
using Membership;
using Threading;

public partial class RaftCluster<TMember>
{
    private interface IMemberList : IReadOnlyDictionary<ClusterMemberId, TMember>
    {
        TMember? LocalMember { get; }
        
        new IReadOnlyCollection<TMember> Values { get; }

        bool TryAdd(TMember member, out IMemberList list);

        TMember? TryRemove(ClusterMemberId id, out IMemberList list);

        internal static IMemberList Empty { get; } = new MemberList();
    }

    private sealed class MemberList : Dictionary<ClusterMemberId, TMember>, IMemberList
    {
        private TMember? localMember;
        
        internal MemberList()
            : base(10)
        {
        }

        private MemberList(MemberList origin)
            : base(origin)
            => localMember = origin.localMember;

        TMember? IMemberList.LocalMember => localMember;

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

        private new bool TryAdd(ClusterMemberId id, TMember member)
        {
            if (!base.TryAdd(id, member))
                return false;
            
            if (!member.IsRemote)
                localMember = member;

            return true;
        }

        TMember? IMemberList.TryRemove(ClusterMemberId id, out IMemberList list)
        {
            MemberList tmp;

            if (ContainsKey(id) && (tmp = new(this)).TryRemove(id) is { } result)
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

        private TMember? TryRemove(ClusterMemberId id)
        {
            if (!Remove(id, out var result))
                return null;

            if (ReferenceEquals(result, localMember))
                localMember = null;

            return result;
        }
    }

    /// <summary>
    /// Indicates that the caller is trying to add or remove cluster member concurrently.
    /// </summary>
    /// <remarks>
    /// The current implementation of Raft doesn't support adding or removing multiple cluster members at a time.
    /// </remarks>
    public sealed class ConcurrentMembershipModificationException : RaftProtocolException
    {
        internal ConcurrentMembershipModificationException()
            : base(ExceptionMessages.ConcurrentMembershipUpdate)
        {
        }
    }

    private readonly AsyncExclusiveLock membershipLock;
    private IMemberList members;
    private InvocationList<Action<RaftCluster<TMember>, RaftClusterMemberEventArgs<TMember>>> memberAddedHandlers, memberRemovedHandlers;

    /// <summary>
    /// Gets the member by its identifier.
    /// </summary>
    /// <param name="id">The identifier of the cluster member.</param>
    /// <returns><see langword="true"/> if member found; otherwise, <see langword="false"/>.</returns>
    protected TMember? TryGetMember(ClusterMemberId id)
        => members.GetValueOrDefault(id);

    /// <summary>
    /// An event raised when new cluster member is detected.
    /// </summary>
    public event Action<RaftCluster<TMember>, RaftClusterMemberEventArgs<TMember>> MemberAdded
    {
        add => memberAddedHandlers += value;
        remove => memberAddedHandlers -= value;
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
        {
            try
            {
                memberAddedHandlers.Invoke(this, new(member));
            }
            catch (Exception e)
            {
                Logger.UnhandledException(e);
            }
        }
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
        {
            try
            {
                memberRemovedHandlers.Invoke(this, new(member));
            }
            catch (Exception e)
            {
                Logger.UnhandledException(e);
            }
        }
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

        var leaderState = LeaderStateOrException;
        var tokenSource = CombineTokens(token, leaderState.Token);
        var process = new ReplicationProcess<TMember>(member, replicationLag)
        {
            Logger = Logger,
            Term = leaderState.Term,
            AuditTrail = AuditTrail,
        };
        var lockTaken = false;
        try
        {
            lockTaken = membershipLock.TryAcquire();
            if (!lockTaken)
                throw new ConcurrentMembershipModificationException();

            var config = await configurationStorage.LoadConfigurationAsync(tokenSource.Token).ConfigureAwait(false);
            if (!IClusterConfiguration<TAddress>.TryAdd(ref config, addressProvider(member)))
                return false;

            // assume that the member is up-to-date with the leader
            member.State.Initialize(AuditTrail);

            // catch up node
            if (!await process.CatchUpAsync(rounds, tokenSource.Token).ConfigureAwait(false))
                return false;

            // make sure that the previous configuration is committed
            var commitIndex = await AuditTrail
                .AppendAsync(new EmptyLogEntry { Term = leaderState.Term }, tokenSource.Token)
                .ConfigureAwait(false);
            leaderState.ForceReplication();
            await AuditTrail.WaitForApplyAsync(commitIndex, tokenSource.Token).ConfigureAwait(false);

            // Append new config to the log (extra empty log entry is required to be sure that other cluster members committed
            // the configuration
            commitIndex = await AuditTrail.AppendAsync(config, leaderState.Term, tokenSource.Token).ConfigureAwait(false);
            leaderState.ForceReplication();

            // ensure that the configuration is committed
            await AuditTrail.WaitForApplyAsync(commitIndex, tokenSource.Token).ConfigureAwait(false);
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
            process.Dispose();
            
            if (lockTaken)
                membershipLock.Release();
        }

        return true;
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
        var leaderState = LeaderStateOrException;
        var lockTaken = false;
        var tokenSource = CombineTokens(token, leaderState.Token);

        try
        {
            lockTaken = membershipLock.TryAcquire();
            if (!lockTaken)
                throw new ConcurrentMembershipModificationException();

            if (members.TryGetValue(id, out var member))
            {
                var config = await configurationStorage.LoadConfigurationAsync(tokenSource.Token).ConfigureAwait(false);
                if (IClusterConfiguration<TAddress>.TryRemove(ref config, addressProvider(member)))
                {
                    // make sure that the previous configuration is committed
                    var commitIndex = await AuditTrail
                        .AppendAsync(new EmptyLogEntry { Term = leaderState.Term }, tokenSource.Token)
                        .ConfigureAwait(false);
                    leaderState.ForceReplication();
                    await AuditTrail.WaitForApplyAsync(commitIndex, tokenSource.Token).ConfigureAwait(false);

                    // append new config to the log
                    commitIndex = await AuditTrail.AppendAsync(config, leaderState.Term, tokenSource.Token).ConfigureAwait(false);
                    leaderState.ForceReplication();
                    await AuditTrail.WaitForApplyAsync(commitIndex, tokenSource.Token).ConfigureAwait(false);
                    return true;
                }
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
            
            if (lockTaken)
                membershipLock.Release();
        }

        return false;
    }

    private async ValueTask ProcessMembershipChangesAsync(IReadOnlySet<TMember> added, IReadOnlySet<TMember> removed)
    {
        var membersCopy = members;
        try
        {
            // remove nodes
            foreach (var member in removed)
            {
                if (ReferenceEquals(member, membersCopy.TryRemove(member.Id, out membersCopy)))
                {
                    OnMemberRemoved(member);
                }
            }
            
            // add nodes
            foreach (var member in added)
            {
                if (membersCopy.TryAdd(member, out membersCopy))
                {
                    OnMemberAdded(member);
                }
            }

            switch (membersCopy.LocalMember)
            {
                case null when members.LocalMember is not null:
                    // local member is removed
                    await MoveToStandbyState(resumable: false).ConfigureAwait(false);
                    break;
                case not null when state is not UnstartedState && members.LocalMember is null:
                    // local member is added
                    await UnfreezeAsync().ConfigureAwait(false);
                    break;
            }

            // rewrite the list of members
            members = membersCopy;
            Interlocked.MemoryBarrierProcessWide();
        }
        finally
        {
            transitionLock.Release();
        }
        
        // stop clients
        foreach (var member in removed)
        {
            try
            {
                await member.CancelPendingRequestsAsync().ConfigureAwait(false);
            }
            finally
            {
                member.Dispose();
            }
        }
    }
    
    /// <summary>
    /// Notifies that the member is unavailable.
    /// </summary>
    /// <remarks>
    /// It's an infrastructure method that can be used to remove unavailable member from the cluster configuration
    /// at the leader side.
    /// </remarks>
    /// <param name="member">The member that is considered as unavailable.</param>
    /// <param name="term">The cluster term at the point in time when the member was detected as unavailable.</param>
    /// <param name="token">The token associated with <see cref="LeadershipToken"/> that identifies the leader state at the time of detection.</param>
    /// <returns>The task representing asynchronous result.</returns>
    protected virtual ValueTask UnavailableMemberDetected(TMember member, long term, CancellationToken token)
        => token.IsCancellationRequested ? ValueTask.FromCanceled(token) : ValueTask.CompletedTask;

    /// <summary>
    /// Provides the helper for implementing <see cref="UnavailableMemberDetected(TMember, long, CancellationToken)"/> method.
    /// </summary>
    /// <param name="configurationStorage">The configuration storage.</param>
    /// <param name="address">The address of the cluster member.</param>
    /// <param name="term">The cluster term at the point in time when the member was detected as unavailable.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="TAddress">The type of the address.</typeparam>
    protected async ValueTask UnavailableMemberDetected<TAddress>(IClusterConfigurationStorage<TAddress> configurationStorage,
        TAddress address,
        long term,
        CancellationToken token)
        where TAddress : notnull
    {
        var config = await configurationStorage.LoadConfigurationAsync(token).ConfigureAwait(false);
        if (IClusterConfiguration<TAddress>.TryRemove(ref config, address))
        {
            await AuditTrail.AppendAsync(config, term, token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Initiates configuration change.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The scope that can be used to report the configuration changes.</returns>
    protected async ValueTask<ConfigurationChangeScope> ChangeConfigurationAsync(CancellationToken token)
    {
        await transitionLock.AcquireAsync(token).ConfigureAwait(false);
        return new(this);
    }

    /// <summary>
    /// Represents configuration change scope.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    protected readonly struct ConfigurationChangeScope : IAsyncDisposable
    {
        private readonly RaftCluster<TMember> cluster;
        private readonly HashSet<TMember> added, removed;

        internal ConfigurationChangeScope(RaftCluster<TMember> cluster)
        {
            this.cluster = cluster;
            added = new();
            removed = new();
        }

        /// <summary>
        /// Marks the member as removed from the configuration.
        /// </summary>
        /// <param name="member">The cluster member marked as removed.</param>
        public void MarkAsRemoved(TMember member)
            => removed.Add(member);

        /// <summary>
        /// Marks the member as added to the configuration.
        /// </summary>
        /// <param name="member">The cluster member marked as added.</param>
        public void MarkAsAdded(TMember member)
            => added.Add(member);

        /// <summary>
        /// Gets a collection of existing members.
        /// </summary>
        public IReadOnlyDictionary<ClusterMemberId, TMember> Members => cluster.members;

        /// <summary>
        /// Closes the scope.
        /// </summary>
        public ValueTask DisposeAsync() => cluster.ProcessMembershipChangesAsync(added, removed);
    }
}