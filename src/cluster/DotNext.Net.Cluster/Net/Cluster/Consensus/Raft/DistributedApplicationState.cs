using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Represents persistent state of distributed application.
    /// </summary>
    /// <remarks>
    /// This class is mandatory in order to support distributed services.
    /// </remarks>
    public partial class DistributedApplicationState : PersistentState
    {
        private readonly bool isOverloaded;

        /// <summary>
        /// Initializes a new persistent storage of distributed application state.
        /// </summary>
        /// <param name="path">The path to the directory used to store </param>
        /// <param name="recordsPerPartition"></param>
        /// <param name="configuration"></param>
        public DistributedApplicationState(DirectoryInfo path, int recordsPerPartition, Options? configuration = null)
            : base(path, recordsPerPartition, configuration)
        {
            lockPersistentStateStorage = new DirectoryInfo(Path.Combine(path.FullName, LockDirectoryName));
            if (!lockPersistentStateStorage.Exists)
                lockPersistentStateStorage.Create();
            acquiredLocks = ImmutableDictionary.Create<string, Threading.DistributedLockInfo>(StringComparer.Ordinal);
            isOverloaded = GetType() != typeof(DistributedApplicationState);
        }

        /// <summary>
        /// Interprets the log entry containing the command related to distributed application management.
        /// </summary>
        /// <param name="command">The command identifier.</param>
        /// <param name="entry">The log entry containing the command.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <seealso cref="LockCommandId"/>
        [CLSCompliant(false)]
        protected ValueTask ApplyAsync(uint command, LogEntry entry) => command switch
        {
            LockCommandId => ApplyLockCommandAsync(entry),
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };

        /// <summary>
        /// Interprets the committed log entry.
        /// </summary>
        /// <remarks>
        /// If you want to override this method then you need to decode the command identifier manually from the log entry and then call <see cref="ApplyAsync(uint, LogEntry)"/>
        /// instead of base implementation.
        /// </remarks>
        /// <param name="entry">The committed log entry.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="NotImplementedException">Attempts to call this method from the derived class.</exception>
        protected override async ValueTask ApplyAsync(LogEntry entry)
        {
            if (isOverloaded)
                throw new NotImplementedException();
            await ApplyAsync(await entry.ReadAsync<uint>().ConfigureAwait(false), entry).ConfigureAwait(false);
        }

        private void ReleaseManagedMemory()
        {
            acquiredLocks.Clear();
            acquireEventSource.Clear();
            releaseEventSource.Clear();
        }

        protected override void Dispose(bool disposing)
        {
            if(disposing)
                ReleaseManagedMemory();
            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            ReleaseManagedMemory();
            return base.DisposeAsync();
        }
    }
}