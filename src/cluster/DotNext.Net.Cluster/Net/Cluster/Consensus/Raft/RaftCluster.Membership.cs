using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Missing = System.Reflection.Missing;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Membership;
    using Threading;

    public partial class RaftCluster<TMember> : IClusterConfigurationStorage.IConfigurationInterpreter, IExpandableCluster
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

            internal MemberList Add(TMember member, out bool added)
            {
                var result = dictionary.Add(member.Id, member);
                if (ReferenceEquals(dictionary, result))
                {
                    added = false;
                    return this;
                }

                added = true;
                return new(result);
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
        /// Represents mutator of a collection of cluster members.
        /// </summary>
        /// <param name="members">The collection of members maintained by instance of <see cref="RaftCluster{TMember}"/>.</param>
        [Obsolete("Use generic version of this delegate")]
        protected delegate void MemberCollectionMutator(in MemberCollectionBuilder members);

        /// <summary>
        /// Represents mutator of a collection of cluster members.
        /// </summary>
        /// <param name="members">The collection of members maintained by instance of <see cref="RaftCluster{TMember}"/>.</param>
        /// <param name="arg">The argument to be passed to the mutator.</param>
        /// <typeparam name="T">The type of the argument.</typeparam>
        [Obsolete("Use appropriate ClusterMemberBootstrap mode in production")]
        protected delegate void MemberCollectionMutator<T>(in MemberCollectionBuilder members, T arg);

        private MemberList members;
        private TaskCompletionSource<bool>? announcementEvent; // TODO: Use non-generic TaskCompletionSource in .NET 6
        private ClusterChangedEventHandler? memberAddedHandlers, memberRemovedHandlers;

        /// <summary>
        /// Finds cluster member using predicate.
        /// </summary>
        /// <param name="criteria">The predicate used to find appropriate member.</param>
        /// <returns>The cluster member; or <see langword="null"/> if there is not member matching to the specified criteria.</returns>
        [Obsolete("Use TryGetMember method instead")]
        protected TMember? FindMember(Predicate<TMember> criteria)
            => members.FirstOrDefault(criteria.AsFunc());

        /// <summary>
        /// Finds cluster member asynchronously using predicate.
        /// </summary>
        /// <param name="criteria">The predicate used to find appropriate member.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The cluster member; or <see langword="null"/> if there is not member matching to the specified criteria.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [Obsolete("Use TryGetMember method instead")]
        protected async ValueTask<TMember?> FindMemberAsync(Func<TMember, CancellationToken, ValueTask<bool>> criteria, CancellationToken token)
        {
            foreach (var candidate in members.Values)
            {
                if (await criteria(candidate, token).ConfigureAwait(false))
                    return candidate;
            }

            return null;
        }

        /// <summary>
        /// Gets the member by its identifier.
        /// </summary>
        /// <param name="id">The identifier of the cluster member.</param>
        /// <returns><see langword="true"/> if member found; otherwise, <see langword="false"/>.</returns>
        protected TMember? TryGetMember(ClusterMemberId id)
            => members.TryGetValue(id, out var result) ? result : null;

        /// <summary>
        /// Finds cluster member using predicate.
        /// </summary>
        /// <typeparam name="TArg">The type of the predicate parameter.</typeparam>
        /// <param name="criteria">The predicate used to find appropriate member.</param>
        /// <param name="arg">The argument to be passed to the matching function.</param>
        /// <returns>The cluster member; or <see langword="null"/> if member doesn't exist.</returns>
        [Obsolete("Use TryGetMember method instead")]
        protected TMember? FindMember<TArg>(Func<TMember, TArg, bool> criteria, TArg arg)
        {
            foreach (var member in members.Values)
            {
                if (criteria(member, arg))
                    return member;
            }

            return null;
        }

        /// <summary>
        /// Modifies collection of cluster members.
        /// </summary>
        /// <typeparam name="T">The type of the argument to be passed to the mutator.</typeparam>
        /// <param name="mutator">The action that can be used to change set of cluster members.</param>
        /// <param name="arg">The argument to be passed to the mutator.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        [Obsolete("Use appropriate ClusterMemberBootstrap mode in production")]
        protected async Task ChangeMembersAsync<T>(MemberCollectionMutator<T> mutator, T arg, CancellationToken token)
        {
            using var tokenSource = token.LinkTo(LifecycleToken);
            using var transitionLock = await transitionSync.TryAcquireAsync(token).SuppressDisposedStateOrCancellation().ConfigureAwait(false);
            if (transitionLock)
                ChangeMembers();

            void ChangeMembers()
            {
                var copy = members;
                mutator(new MemberCollectionBuilder(ref copy), arg);
                members = copy;
            }
        }

        /// <summary>
        /// Modifies collection of cluster members.
        /// </summary>
        /// <param name="mutator">The action that can be used to change set of cluster members.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        [Obsolete("Use generic version of this method")]
        protected Task ChangeMembersAsync(MemberCollectionMutator mutator, CancellationToken token)
            => ChangeMembersAsync((in MemberCollectionBuilder builder, Missing arg) => mutator(in builder), Missing.Value, token);

        /// <summary>
        /// Modifies collection of cluster members.
        /// </summary>
        /// <param name="mutator">The action that can be used to change set of cluster members.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        [Obsolete("Use generic version of this method")]
        protected Task ChangeMembersAsync(MemberCollectionMutator mutator)
            => ChangeMembersAsync(mutator, CancellationToken.None);

        /// <summary>
        /// Announces a new node.
        /// </summary>
        /// <remarks>
        /// The identifier of the local node to be announced is available via <see cref="LocalMemberId"/> property.
        /// </remarks>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous result.</returns>
        protected abstract Task AnnounceAsync(CancellationToken token = default);

        /// <summary>
        /// Commits the local member to the storage.
        /// </summary>
        /// <remarks>
        /// The identifier of the local node to be committed is available via <see cref="LocalMemberId"/> property.
        /// You can use <see cref="Membership.AddMemberLogEntry"/> log entry to commit the address of the local member.
        /// </remarks>
        /// <param name="appender">The delegate that can be used to add the address of the local member.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous result.</returns>
        protected abstract ValueTask AddLocalMemberAsync(Func<Membership.AddMemberLogEntry, CancellationToken, ValueTask<long>> appender, CancellationToken token);

        /// <summary>
        /// Creates a client for the cluster member.
        /// </summary>
        /// <remarks>
        /// This method is called automatically when a new member is added to the log.
        /// </remarks>
        /// <param name="id">The identifier of the cluster member.</param>
        /// <param name="address">The address of the cluster member in raw format.</param>
        /// <returns>The client for the cluster member.</returns>
        protected abstract TMember CreateMember(in ClusterMemberId id, ReadOnlyMemory<byte> address);

        /// <inheritdoc />
        ValueTask IClusterConfigurationStorage.IConfigurationInterpreter.AddMemberAsync(ClusterMemberId id, ReadOnlyMemory<byte> address)
        {
            var result = new ValueTask();
            try
            {
                var member = CreateMember(id, address);
                members = members.Add(member, out var added);
                if (added)
                {
                    if (id == localMemberId)
                        announcementEvent?.TrySetResult(true);

                    // raise event
                    OnMemberAdded(member);
                }
                else
                {
                    member.Dispose();
                }
            }
            catch (Exception e)
            {
#if NETSTANDARD2_1
                result = new(Task.FromException(e));
#else
                result = ValueTask.FromException(e);
#endif
            }

            return result;
        }

        /// <summary>
        /// An event raised when new cluster member is detected.
        /// </summary>
        public event ClusterChangedEventHandler MemberAdded
        {
            add => memberAddedHandlers += value;
            remove => memberRemovedHandlers -= value;
        }

        /// <summary>
        /// Invokes all registered handlers of <see cref="MemberAdded"/> event.
        /// </summary>
        /// <param name="member">The added member.</param>
        protected void OnMemberAdded(TMember member) // TODO: Must be private in .NEXT 4
            => memberAddedHandlers?.Invoke(this, member);

        /// <inheritdoc />
        async ValueTask IClusterConfigurationStorage.IConfigurationInterpreter.RemoveMemberAsync(ClusterMemberId id)
        {
            if (MemberList.TryRemove(ref members, id, out var member))
            {
                using (member)
                {
                    // local member is to be removed, downgrade it to standby state
                    if (id == localMemberId)
                    {
                        var newState = new StandbyState(this);
                        using (var currentState = state)
                        {
                            await (currentState?.StopAsync() ?? Task.CompletedTask).ConfigureAwait(false);
                            state = newState;
                        }
                    }

                    // raise event
                    OnMemberRemoved(member);
                }
            }
        }

        /// <summary>
        /// An event raised when cluster member is removed gracefully.
        /// </summary>
        public event ClusterChangedEventHandler MemberRemoved
        {
            add => memberRemovedHandlers += value;
            remove => memberRemovedHandlers -= value;
        }

        /// <summary>
        /// Invokes all registered handlers of <see cref="MemberRemoved"/> event.
        /// </summary>
        /// <param name="member">The added member.</param>
        protected void OnMemberRemoved(TMember member) // TODO: Must be private in .NEXT 4
            => memberRemovedHandlers?.Invoke(this, member);

        /// <inheritdoc />
        async ValueTask IClusterConfigurationStorage.IConfigurationInterpreter.RefreshAsync(IAsyncEnumerable<KeyValuePair<ClusterMemberId, ReadOnlyMemory<byte>>> members, CancellationToken token)
        {
            var fresh = new Dictionary<ClusterMemberId, TMember>();

            // 1. Add all members from the input
            await foreach (var (id, address) in members.WithCancellation(token))
            {
                if (!this.members.TryGetValue(id, out var member))
                {
                    member = CreateMember(id, address);

                    if (id == localMemberId)
                        announcementEvent?.TrySetResult(true);

                    OnMemberAdded(member);
                }

                if (!fresh.TryAdd(id, member))
                    member.Dispose();
            }

            // 2. Destroy members that are not from the fresh list
            foreach (var member in this.members.Values)
            {
                if (!fresh.ContainsKey(member.Id))
                {
                    using (member)
                    {
                        if (localMemberId == member.Id)
                        {
                            var newState = new StandbyState(this);
                            using (var currentState = state)
                            {
                                await (currentState?.StopAsync() ?? Task.CompletedTask).ConfigureAwait(false);
                                state = newState;
                            }
                        }

                        OnMemberRemoved(member);
                    }
                }

                token.ThrowIfCancellationRequested();
            }

            // 3. Replace the list of members
            this.members = new(fresh);
            fresh.Clear();
        }

        /// <summary>
        /// Adds member in <see cref="ClusterMemberBootstrap.Announcement"/> state
        /// to the cluster.
        /// </summary>
        /// <param name="id">The identifier of the cluster node.</param>
        /// <param name="address">The address of the cluster node, in raw format.</param>
        /// <param name="rounds">The number of warmup rounds.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>
        /// <see langword="true"/> if the node has been added to the cluster successfully;
        /// <see langword="false"/> if the node rejects the replication or the address of the node cannot be committed.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="rounds"/> is less than or equal to zero.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled or the cluster elects a new leader.</exception>
        protected async Task<bool> AddMemberAsync(ClusterMemberId id, ReadOnlyMemory<byte> address, int rounds, CancellationToken token = default)
        {
            if (rounds <= 0)
                throw new ArgumentOutOfRangeException(nameof(rounds));

            using var tokenSource = token.LinkTo(LeadershipToken);

            // this client is used only for warmup process
            using (var tempClient = CreateMember(id, address))
            {
                // catch up node
                long currentIndex;

                do
                {
                    var commitIndex = auditTrail.GetLastIndex(true);
                    currentIndex = auditTrail.GetLastIndex(false);
                    var precedingIndex = Math.Max(0, tempClient.NextIndex - 1);
                    var precedingTerm = await auditTrail.GetTermAsync(precedingIndex, token).ConfigureAwait(false);
                    var term = Term;

                    // do replication
                    var result = await new LeaderState.Replicator(auditTrail, tempClient, commitIndex, currentIndex, term, precedingIndex, precedingTerm, Logger, token).ReplicateAsync(false).ConfigureAwait(false);

                    if (!result.Value)
                        return false;
                }
                while (rounds > 0 && currentIndex >= tempClient.NextIndex);
            }

            // append new configuration entry to the log, commit and replicate it
            return await ReplicateAsync(new AddMemberLogEntry(id, address, Term), Timeout.Infinite, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Removes member from the cluster.
        /// </summary>
        /// <param name="id">The identifier of the node to be removed.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>
        /// <see langword="true"/> if node removed successfully;
        /// <see langword="false"/> if the current node is unable to commit removal command.
        /// </returns>
        public async Task<bool> RemoveMemberAsync(ClusterMemberId id, CancellationToken token = default)
        {
            using var tokenSource = token.LinkTo(LeadershipToken);

            return await ReplicateAsync(new RemoveMemberLogEntry(id, Term), Timeout.Infinite, token).ConfigureAwait(false);
        }
    }
}