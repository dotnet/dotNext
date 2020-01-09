using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Buffers;
    using DistributedServices;
    using IO;
    using IO.Log;
    using Text;
    using Threading;
    using FalseTask = Threading.Tasks.CompletedTask<bool, Generic.BooleanConst.False>;

    public partial class DistributedApplicationState : IDistributedLockEngine
    {
        //each file in directory contains description of distributed lock
        //file name is hex encoded lock name to avoid case sensitivity issues 
        private const int MaxLockNameLength = 63;   //255 max file name / 4 bytes per character ~ 63
        private const int BufferSize = 1024;
        private const string LockDirectoryName = "locks";

        /// <summary>
        /// Gets the prefix of log entry that represents
        /// distributed lock command.
        /// </summary>
        [CLSCompliant(false)]
        protected const uint LockCommandId = 0xedb88320;

        //Release=>Acquire=>Release... sequence allows to replicate 
        //distributed lock state across cluster nodes
        private enum LockCommand : short
        {
            Nop = 0,
            Acquire,
            Release
        }

        private readonly struct ReleaseLockCommand : IWriteOnlyLogEntry
        {
            private readonly long term;
            private readonly ReadOnlyMemory<char> name;
            private readonly DateTimeOffset timestamp;

            internal ReleaseLockCommand(string name, long term)
            {
                this.term = term;
                timestamp = DateTimeOffset.UtcNow;
                this.name = name.AsMemory();
            }

            long IRaftLogEntry.Term => term;

            DateTimeOffset ILogEntry.Timestamp => timestamp;

            async ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            {
                await writer.WriteAsync(LockCommand.Release, token).ConfigureAwait(false);
                var context = new EncodingContext(Encoding.UTF8, true);
                await writer.WriteAsync(name, context, StringLengthEncoding.Plain, token).ConfigureAwait(false);
            }

            internal static async ValueTask<string> ReadAsync(LogEntry entry)
            {
                var context = new DecodingContext(Encoding.UTF8, true);
                return await entry.ReadStringAsync(StringLengthEncoding.Plain, context).ConfigureAwait(false);
            }
        }

        [StructLayout(LayoutKind.Auto)]
        private readonly struct AcquireLockCommand : IWriteOnlyLogEntry
        {
            private readonly long term;
            private readonly DateTimeOffset timestamp;
            private readonly ReadOnlyMemory<char> name;
            private readonly ClusterMemberId owner;
            private readonly Guid version;
            private readonly TimeSpan leaseTime;

            internal AcquireLockCommand(string name, DistributedLock lockInfo, long term)
            {
                this.term = term;
                timestamp = lockInfo.CreationTime;
                this.name = name.AsMemory();
                owner = lockInfo.Owner;
                version = lockInfo.Version;
                leaseTime = lockInfo.LeaseTime;
            }

            long IRaftLogEntry.Term => term;

            DateTimeOffset ILogEntry.Timestamp => timestamp;

            async ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            {
                await writer.WriteAsync(LockCommand.Acquire, token).ConfigureAwait(false);
                var context = new EncodingContext(Encoding.UTF8, true);
                await writer.WriteAsync(name, context, StringLengthEncoding.Plain, token).ConfigureAwait(false);
                await writer.WriteAsync(owner, token).ConfigureAwait(false);
                await writer.WriteAsync(version, token).ConfigureAwait(false);
                await writer.WriteAsync(leaseTime, token).ConfigureAwait(false);
            }

            internal static async ValueTask<(string, DistributedLock)> ReadAsync(LogEntry entry)
            {
                var context = new DecodingContext(Encoding.UTF8, true);
                (string Name, DistributedLock Info) lockData;
                lockData.Name = await entry.ReadStringAsync(StringLengthEncoding.Plain, context).ConfigureAwait(false);
                lockData.Info = new DistributedLock
                {
                    Owner = await entry.ReadAsync<ClusterMemberId>().ConfigureAwait(false),
                    Version = await entry.ReadAsync<Guid>().ConfigureAwait(false),
                    LeaseTime = await entry.ReadAsync<TimeSpan>().ConfigureAwait(false)
                };
                return lockData;
            }
        }

        /// <summary>
        /// Represents interpreter of distributed lock management commands.
        /// </summary>
        /// <remarks>
        /// This type should be inside of <see cref="PersistentState.SnapshotBuilder"/> custom implementation.
        /// </remarks>
        protected readonly struct LockSnapshotBuilder : IDisposable
        {
            private readonly Dictionary<string, DistributedLock> table;

            internal LockSnapshotBuilder(int count, IEqualityComparer<string> lockNameComparer) => table = new Dictionary<string, DistributedLock>(count, lockNameComparer);

            private static async ValueTask ApplyAcquireLockCommandAsync(IDictionary<string, DistributedLock> table, LogEntry entry)
            {
                var (lockName, lockInfo) = await AcquireLockCommand.ReadAsync(entry).ConfigureAwait(false);
                table[lockName] = lockInfo;
            }

            private static async ValueTask ApplyReleaseLockCommandAsync(IDictionary<string, DistributedLock> table, LogEntry entry) => table.Remove(await ReleaseLockCommand.ReadAsync(entry).ConfigureAwait(false));

            private static async ValueTask InstallEntryAsync(IDictionary<string, DistributedLock> table, LogEntry entry)
            {
                var command = await entry.ReadAsync<LockCommand>().ConfigureAwait(false);
                var task = command switch
                {
                    LockCommand.Nop => new ValueTask(),
                    LockCommand.Acquire => ApplyAcquireLockCommandAsync(table, entry),
                    LockCommand.Release => ApplyReleaseLockCommandAsync(table, entry),
                    _ => throw new InvalidDataException()
                };
                await task.ConfigureAwait(false);
            }

            /// <summary>
            /// Appens a new command to this builder.
            /// </summary>
            /// <param name="entry">The log entry containing lock management command.</param>
            /// <returns>The task representing state of asynchronous execution.</returns>
            /// <exception cref="InvalidDataException"><paramref name="entry"/> is corrupted.</exception>
            public ValueTask AppendAsync(LogEntry entry) => entry.IsSnapshot ? ApplyLockSnapshotAsync(table, entry) : InstallEntryAsync(table, entry);

            private static async ValueTask WriteToAsync<TWriter>(TWriter writer, IReadOnlyDictionary<string, DistributedLock> table, CancellationToken token)
                where TWriter : IAsyncBinaryWriter
            {
                //implementation should be in sync with ApplyLockSnapshotAsync method
                await writer.WriteAsync(table.Count, token).ConfigureAwait(false);
                var context = new EncodingContext(Encoding.UTF8, true);
                foreach (var (name, info) in table)
                {
                    await writer.WriteAsync(name.AsMemory(), context, StringLengthEncoding.Plain, token).ConfigureAwait(false);
                    await writer.WriteAsync(info, token).ConfigureAwait(false);
                }
            }

            /// <summary>
            /// Writes captured snapshot of distributed locks.
            /// </summary>
            /// <typeparam name="TWriter">The type of the binary writer.</typeparam>
            /// <param name="writer">The binary writer.</param>
            /// <param name="token">The token that can be used to cancel the operation.</param>
            /// <returns>The task representing state of asynchronous execution.</returns>
            public ValueTask WriteToAsync<TWriter>(TWriter writer, CancellationToken token) where TWriter : IAsyncBinaryWriter => WriteToAsync(writer, table, token);

            /// <summary>
            /// Releases all resources associated with this builder.
            /// </summary>
            public void Dispose() => table?.Clear();
        }

        //copy-on-write semantics
        private volatile ImmutableDictionary<string, DistributedLock> acquiredLocks = ImmutableDictionary.Create<string, DistributedLock>(StringComparer.Ordinal, BitwiseComparer<DistributedLock>.Instance);
        private readonly DirectoryInfo lockPersistentStateStorage;
        private readonly AsyncManualResetEvent releaseEvent = new AsyncManualResetEvent(false);
        private readonly AsyncManualResetEvent acquireEvent = new AsyncManualResetEvent(false);

        private void RemoveLock(string lockName)
        {
            acquiredLocks = acquiredLocks.Remove(lockName);
            RemoveLockFile(lockName);
            releaseEvent.Set(true);
        }

        private async ValueTask ApplyAcquireLockCommandAsync(LogEntry entry)
        {
            var (lockName, lockInfo) = await AcquireLockCommand.ReadAsync(entry).ConfigureAwait(false);
            if (lockInfo.IsExpired)
                RemoveLock(lockName);
            else
            {
                acquiredLocks = acquiredLocks.SetItem(lockName, lockInfo);
                //save lock state to file
                await SaveLockAsync(lockName, lockInfo).ConfigureAwait(false);
                acquireEvent.Set(true);
            }
        }

        private async ValueTask ApplyReleaseLockCommandAsync(LogEntry entry)
            => RemoveLock(await ReleaseLockCommand.ReadAsync(entry).ConfigureAwait(false));

        private async ValueTask ApplyLockCommandAsync(LogEntry entry)
        {
            var command = await entry.ReadAsync<LockCommand>().ConfigureAwait(false);
            ValueTask task = command switch
            {
                LockCommand.Nop => new ValueTask(),
                LockCommand.Acquire => ApplyAcquireLockCommandAsync(entry),
                LockCommand.Release => ApplyReleaseLockCommandAsync(entry),
                _ => throw new InvalidDataException()
            };
            await task.ConfigureAwait(false);
        }

        private static async ValueTask ApplyLockSnapshotAsync(IDictionary<string, DistributedLock> output, LogEntry entry)
        {
            //should be in sync with LockSnapshotBuilder.WriteAsync method
            var count = await entry.ReadAsync<int>().ConfigureAwait(false);
            var context = new DecodingContext(Encoding.UTF8, true);
            while (count-- > 0)
            {
                var name = await entry.ReadStringAsync(StringLengthEncoding.Plain, context).ConfigureAwait(false);
                var info = await entry.ReadAsync<DistributedLock>().ConfigureAwait(false);
                output[name] = info;
            }
        }

        /// <summary>
        /// Installs snapshot of all distributed locks atomically.
        /// </summary>
        /// <remarks>
        /// This method should be called inside of <see cref="ApplyAsync(LogEntry)"/>
        /// method if <see cref="PersistentState.LogEntry.IsSnapshot"/> equals to <see langword="true"/>. 
        /// </remarks>
        /// <param name="snapshot">The log entry adjusted to the snapshot of distributed locks.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        protected async ValueTask ApplyLockSnapshotAsync(LogEntry snapshot)
        {
            var builder = ImmutableDictionary.CreateBuilder<string, DistributedLock>(acquiredLocks.KeyComparer);
            await ApplyLockSnapshotAsync(builder, snapshot).ConfigureAwait(false);
            //remove  all locks from target table
            foreach(var name in acquiredLocks.Keys)
                if(!builder.ContainsKey(name))
                    RemoveLockFile(name);
            //install locks from snapshot
            foreach(var (name, info) in builder)
                await SaveLockAsync(name, info).ConfigureAwait(false);
            acquiredLocks = builder.ToImmutable();
            releaseEvent.Set(true);
            acquireEvent.Set(true);
            builder.Clear();    //help GC
        }

        Task<bool> IDistributedLockEngine.WaitForLockEventAsync(bool acquireEvent, TimeSpan timeout, CancellationToken token)
            => acquireEvent ? this.acquireEvent.WaitAsync(timeout, token) : releaseEvent.WaitAsync(timeout, token);

        async Task IDistributedLockEngine.ProvideSponsorshipAsync<TSponsor>(TSponsor sponsor, CancellationToken token)
        {
            using var writeLock = await AcquireWriteLockAsync(token).ConfigureAwait(false);
            var acquiredLocks = this.acquiredLocks;
            var builder = acquiredLocks.ToBuilder();
            bool modified = false, released = false;
            using(var enumerator = acquiredLocks.GetEnumerator())
                while(enumerator.MoveNext())
                {
                    var (name, info) = enumerator.Current;
                    switch(sponsor.UpdateLease(ref info))
                    {
                        case LeaseState.Expired:
                            modified = true;
                            released = true;
                            builder.Remove(name);
                            RemoveLockFile(name);
                            continue;
                        case LeaseState.Prolonged:
                            modified = true;
                            builder[name] = info;
                            continue;
                    }
                }
            if(modified)
                this.acquiredLocks = builder.ToImmutable();
            if(released)
                releaseEvent.Set(true);
            builder.Clear();    //help GC
        }

        private async Task<bool> RegisterAsync(string name, DistributedLock newLock, CancellationToken token)
        {
            using var writeLock = await AcquireWriteLockAsync(token).ConfigureAwait(false);
            if (acquiredLocks.TryGetValue(name, out var existingLock) && !existingLock.IsExpired)
                return false;
            acquiredLocks = acquiredLocks.SetItem(name, newLock);    
            //log entry can be added only out of write-lock scope
            await AppendAsync(writeLock, new AcquireLockCommand(name, newLock, Term), token).ConfigureAwait(false);
            return true;
        }

        Task<bool> IDistributedLockEngine.RegisterAsync(string name, DistributedLock lockInfo, CancellationToken token)
            => lockInfo.IsExpired ? FalseTask.Task : RegisterAsync(name, lockInfo, token);

        async Task<bool> IDistributedLockEngine.UnregisterAsync(string name, ClusterMemberId owner, Guid version, CancellationToken token)
        {
            using var writeLock = await AcquireWriteLockAsync(token).ConfigureAwait(false);
            if (acquiredLocks.TryGetValue(name, out var existingLock) && existingLock.Owner == owner && existingLock.Version == version)
            {
                acquiredLocks = acquiredLocks.Remove(name);
                //log entry can be added only out of write-lock scope
                await AppendAsync(writeLock, new ReleaseLockCommand(name, Term), token).ConfigureAwait(false);
                return true;
            }
            return false;
        }

        async Task IDistributedLockEngine.UnregisterAsync(string name, CancellationToken token)
        {
            using var writeLock = await AcquireWriteLockAsync(token).ConfigureAwait(false);
#pragma warning disable CS0420
            if (ImmutableInterlocked.TryRemove(ref acquiredLocks, name, out _))
                await AppendAsync(writeLock, new ReleaseLockCommand(name, Term), token).ConfigureAwait(false);
#pragma warning restore CS0420
        }

        /// <summary>
        /// Creates a new 
        /// </summary>
        /// <remarks>
        /// This method should be called inside of <see cref="PersistentState.CreateSnapshotBuilder"/> method
        /// to construct separated builder for the distributed lock table.
        /// </remarks>
        /// <returns>A new instance of snapshot builder.</returns>
        protected LockSnapshotBuilder CreateLockSnapshotBuilder()
        {
            var acquiredLocks = this.acquiredLocks;
            return new LockSnapshotBuilder(acquiredLocks.Count, acquiredLocks.KeyComparer);
        }

        bool IDistributedLockEngine.IsRegistered(string lockName, in ClusterMemberId owner, in Guid version)
            => acquiredLocks.TryGetValue(lockName, out var lockInfo) && lockInfo.Version == version && lockInfo.Owner == owner;

        private static string FileNameToLockName(string fileName) 
        {
            //4 characters = 1 decoded Unicode character
            var count = fileName.Length / 4;
            if(count == 0)
                return string.Empty;
            if(count >= MaxLockNameLength)
                throw new ArgumentOutOfRangeException(nameof(fileName), ExceptionMessages.LockNameTooLong);
            Span<char> chars = stackalloc char[count];
            count = fileName.AsSpan().FromHex(MemoryMarshal.AsBytes(chars)) / 2;
            return new string(chars.Slice(0, count));
        }

        private static string LockNameToFileName(string lockName) 
            => lockName.Length <= MaxLockNameLength ? MemoryMarshal.AsBytes(lockName.AsSpan()).ToHex() : throw new ArgumentOutOfRangeException(nameof(lockName), ExceptionMessages.LockNameTooLong);

        private static string LockNameToFileName(DirectoryInfo lockStorage, string lockName)
            => Path.Combine(lockStorage.FullName, LockNameToFileName(lockName));

        private async ValueTask SaveLockAsync(string lockName, DistributedLock lockInfo)
        {
            lockName = LockNameToFileName(lockPersistentStateStorage, lockName);
            using var lockFile = new FileStream(lockName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, BufferSize, true);
            //lockInfo has fixed size so no need to truncate file stream
            await lockFile.WriteAsync(lockInfo).ConfigureAwait(false);
            await lockFile.FlushAsync().ConfigureAwait(false);
        }

        private void RemoveLockFile(string lockName) => File.Delete(LockNameToFileName(lockPersistentStateStorage, lockName));

        void IDistributedLockEngine.ValidateName(string name)
        {
            if (name.Length == 0)
                throw new ArgumentException(ExceptionMessages.LockNameIsEmpty, nameof(name));
            if (name.Length > MaxLockNameLength)
                throw new ArgumentOutOfRangeException(nameof(name), ExceptionMessages.LockNameTooLong);
        }

        async Task IDistributedLockEngine.RestoreAsync(CancellationToken token)
        {
            var builder = acquiredLocks.ToBuilder();
            //restore lock state from file system
            const int fileBuffer = 1024;
            using var buffer = new ArrayRental<byte>(fileBuffer);
            foreach (var lockFile in lockPersistentStateStorage.EnumerateFiles())
                using (var fs = new FileStream(lockFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, true))
                {
                    var state = await fs.ReadAsync<DistributedLock>(buffer.Memory, token).ConfigureAwait(false);
                    builder.Add(FileNameToLockName(lockFile.Name), state);
                }
            acquiredLocks = builder.ToImmutable();
        }
    }
}