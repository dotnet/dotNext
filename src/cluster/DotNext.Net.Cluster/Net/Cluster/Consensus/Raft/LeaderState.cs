using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Net.Cluster.Replication;
using DotNext.Threading;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    internal sealed class LeaderState : RaftState
    {
        private BackgroundTask heartbeatTask;

        internal LeaderState(IRaftStateMachine stateMachine)
            : base(stateMachine)
        {
        }

        private async Task DoHeartbeats(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                ICollection<Task> tasks = new LinkedList<Task>();
                //send heartbeat in parallel
                foreach (var member in stateMachine.Members)
                    tasks.Add(member.HeartbeatAsync(token));
                await Task.WhenAll(tasks).ConfigureAwait(false);
                tasks.Clear();
            }
        }

        private static async Task AppendEntriesAsync(IRaftClusterMember member, ILogEntry newEntry,
            LogEntryId lastEntry, IAuditTrail transactionLog, CancellationToken token)
        {
            if (await member.AppendEntriesAsync(newEntry, lastEntry, token).ConfigureAwait(false))
                return;
            //unable to commit fresh entry, tries to commit older entries
            var lookup = lastEntry == transactionLog.Initial
                ? throw new ReplicationException(member)
                : transactionLog[lastEntry];
            while (!await member.AppendEntriesAsync(lookup, transactionLog.GetPrevious(lookup.Id), token)
                .ConfigureAwait(false))
            {
                lookup = transactionLog.GetPrevious(lookup);
                if (lookup is null || lookup.Id == transactionLog.Initial)
                    throw new ReplicationException(member);
            }

            //now lookup is committed, try to commit all new entries
            lookup = transactionLog.GetNext(lookup);
            while (lookup != null)
                if (await member.AppendEntriesAsync(lookup, transactionLog.GetPrevious(lookup.Id), token)
                    .ConfigureAwait(false))
                    lookup = transactionLog.GetNext(lookup);
                else
                    throw new ReplicationException(member);
            //now transaction log is restored on remote side, commit the fresh entry
            if (!await member.AppendEntriesAsync(newEntry, lastEntry, token).ConfigureAwait(false))
                throw new ReplicationException(member);
        }

        internal async Task AppendEntriesAsync(ILogEntry entry, IAuditTrail transactionLog, CancellationToken token)
        {
            using (var tokenSource = heartbeatTask.CreateLinkedTokenSource(token))
            {
                var lastEntry = transactionLog.LastRecord;
                ICollection<Task> tasks = new LinkedList<Task>();
                foreach (var member in stateMachine.Members)
                    if (member.IsRemote)
                        tasks.Add(AppendEntriesAsync(member, entry, lastEntry, transactionLog, tokenSource.Token));
                await Task.WhenAll(tasks).ConfigureAwait(false);
                //now the record is accepted by other nodes, commit it locally
                await transactionLog.CommitAsync(entry).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Starts cluster synchronization.
        /// </summary>
        internal void StartLeading()
            => heartbeatTask = new BackgroundTask(DoHeartbeats);
        internal Task StopLeading() => heartbeatTask.Stop();

        internal Task StopLeading(CancellationToken token) => heartbeatTask.Stop(token);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                heartbeatTask.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
