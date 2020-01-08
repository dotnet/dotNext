using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using DistributedLock = DistributedServices.DistributedLock;

    /// <summary>
    /// Represents persistent state of distributed application.
    /// </summary>
    /// <remarks>
    /// This class is mandatory in order to support distributed services.
    /// Derived class must override <see cref="ApplyAsync(PersistentState.LogEntry)"/>
    /// and <see cref="CreateSnapshotBuilder"/> methods.
    /// </remarks>
    public partial class DistributedApplicationState : PersistentState
    {
        private sealed class DefaultSnapshotBuilder : SnapshotBuilder
        {
            private readonly LockSnapshotBuilder lockBuilder;

            internal DefaultSnapshotBuilder(LockSnapshotBuilder lockBuilder)
            {
                this.lockBuilder = lockBuilder;
            }

            protected override ValueTask ApplyAsync(LogEntry entry)
                => lockBuilder.AppendAsync(entry);

            public override ValueTask WriteToAsync<TWriter>(TWriter writer, System.Threading.CancellationToken token)
                => lockBuilder.WriteToAsync(writer, token);
            
            protected override void Dispose(bool disposing)
            {
                if(disposing)
                    lockBuilder.Dispose();
                base.Dispose(disposing);
            }
        }

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
            isOverloaded = GetType() != typeof(DistributedApplicationState);
        }

        /// <summary>
        /// Interprets the log entry containing the command related to distributed application management.
        /// </summary>
        /// <param name="command">The command identifier.</param>
        /// <param name="entry">The log entry containing the command.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="command"/> is invalid command identifier.</exception>
        /// <exception cref="InvalidDataException"><paramref name="entry"/> is corrupted.</exception>
        /// <seealso cref="LockCommandId"/>
        [CLSCompliant(false)]
        protected ValueTask ApplyAsync(uint command, LogEntry entry) => command switch
        {
            LockCommandId => ApplyLockCommandAsync(entry),
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };

        /// <summary>
        /// Interprets the committed log entry.
        /// </summary>
        /// <remarks>
        /// Derived class must override this method
        /// and call <see cref="ApplyAsync(uint, LogEntry)"/> instead
        /// of base implementation.
        /// </remarks>
        /// <param name="entry">The committed log entry.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="NotImplementedException">Attempts to call this method from the derived class.</exception>
        protected override async ValueTask ApplyAsync(LogEntry entry)
        {
            if (isOverloaded)
                throw new NotImplementedException();
            if(entry.IsSnapshot)
                await ApplyLockSnapshotAsync(entry).ConfigureAwait(false);
            else
                await ApplyAsync(await entry.ReadAsync<uint>().ConfigureAwait(false), entry).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a new snapshot builder.
        /// </summary>
        /// <remarks>
        /// Derived class must override this method and do not call
        /// base implementation.
        /// </remarks>
        /// <returns>The snapshot builder.</returns>
        protected override SnapshotBuilder CreateSnapshotBuilder()
        {
            if(isOverloaded)
                throw new NotImplementedException();
            return new DefaultSnapshotBuilder(CreateLockSnapshotBuilder());
        }

        private void ReleaseManagedMemory()
        {
            acquiredLocks = ImmutableDictionary<string, DistributedLock>.Empty;
            acquireEvent.Dispose();
            releaseEvent.Dispose();
        }

        /// <summary>
        /// Releases all resources associated with this application state.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> if called from <see cref="IDisposable.Dispose()"/>; <see langword="false"/> if called from finalizer.</param>
        protected override void Dispose(bool disposing)
        {
            if(disposing)
                ReleaseManagedMemory();
            base.Dispose(disposing);
        }
        
        /// <summary>
        /// Releases unmanaged resources asynchronously.
        /// </summary>
        /// <returns>A task representing state of asynchronous execution.</returns>
        public override ValueTask DisposeAsync()
        {
            ReleaseManagedMemory();
            return base.DisposeAsync();
        }
    }
}