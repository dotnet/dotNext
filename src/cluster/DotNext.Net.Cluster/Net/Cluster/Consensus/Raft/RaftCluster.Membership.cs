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
    using Threading;

    public partial class RaftCluster<TMember>
    {
        internal sealed class MemberList : IReadOnlyDictionary<ClusterMemberId, TMember>, IReadOnlyCollection<TMember>
        {
            internal static readonly MemberList Empty = new();

            private readonly ImmutableDictionary<ClusterMemberId, TMember> dictionary;

            internal MemberList(IEnumerable<TMember> members)
            {
                var builder = ImmutableDictionary.CreateBuilder<ClusterMemberId, TMember>();

                foreach (var member in members)
                    builder.Add(member.Id, member);

                dictionary = builder.ToImmutable();
            }

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

            internal MemberList Add(TMember member) => new(dictionary.Add(member.Id, member));

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
        /// Generates a new unique cluster member identifier.
        /// </summary>
        /// <returns>Generated cluster member identifier.</returns>
        protected ClusterMemberId NewClusterMemberId() => new(random);

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
    }
}