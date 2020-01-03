using System;
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
            //it is struct for faster serialization with writer
            private readonly Guid owner, version;
            private readonly TimeSpan leaseTime;

            internal AcquireLockCommand(string name, DistributedLockInfo lockInfo, long term)
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

            internal static async ValueTask<(string, DistributedLockInfo)> ReadAsync(LogEntry entry)
            {
                var context = new DecodingContext(Encoding.UTF8, true);
                (string Name, DistributedLockInfo Info) lockData;
                lockData.Name = await entry.ReadStringAsync(StringLengthEncoding.Plain, context).ConfigureAwait(false);
                lockData.Info = new DistributedLockInfo
                {
                    Owner = await entry.ReadAsync<Guid>().ConfigureAwait(false),
                    Version = await entry.ReadAsync<Guid>().ConfigureAwait(false),
                    LeaseTime = await entry.ReadAsync<TimeSpan>().ConfigureAwait(false)
                };
                return lockData;
            }
        }

        //copy-on-write semantics
        private volatile ImmutableDictionary<string, DistributedLockInfo> acquiredLocks;
        private readonly DirectoryInfo lockPersistentStateStorage;
        private readonly AsyncEventSource releaseEventSource = new AsyncEventSource();
        private readonly AsyncEventSource acquireEventSource = new AsyncEventSource();

        private void RemoveLock(string lockName)
        {
            acquiredLocks = acquiredLocks.Remove(lockName);
            RemoveLockFile(lockName);
            releaseEventSource.Resume();
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
                acquireEventSource.Resume();
            }
        }

        private async ValueTask ApplyReleaseLockCommandAsync(LogEntry entry)
            => RemoveLock(await ReleaseLockCommand.ReadAsync(entry).ConfigureAwait(false));

        private async ValueTask ApplyLockCommandAsync(LogEntry entry)
        {
            var command = await entry.ReadAsync<LockCommand>().ConfigureAwait(false);
            ValueTask task = command switch
            {
                LockCommand.Acquire => ApplyAcquireLockCommandAsync(entry),
                LockCommand.Release => ApplyReleaseLockCommandAsync(entry),
                _ => throw new InvalidOperationException()
            };
            await task.ConfigureAwait(false);
        }

        AsyncEventListener IDistributedLockEngine.OnRelease(CancellationToken token) => new AsyncEventListener(releaseEventSource, token);

        AsyncEventListener IDistributedLockEngine.OnAcquire(CancellationToken token) => new AsyncEventListener(acquireEventSource, token);

        Task IDistributedLockEngine.CollectGarbage(CancellationToken token) => Task.CompletedTask;

        private async Task<bool> ReportAcquisition(string name, DistributedLockInfo newLock, CancellationToken token)
        {
            using (await WriteLock.AcquireAsync(token).ConfigureAwait(false))
                if (acquiredLocks.TryGetValue(name, out var existingLock) && !existingLock.IsExpired)
                    return false;
                else
                    acquiredLocks = acquiredLocks.SetItem(name, newLock);
            //log entry can be added only out of write-lock scope
            await AppendAsync(new AcquireLockCommand(name, newLock, Term), token).ConfigureAwait(false);
            return true;
        }

        Task<bool> IDistributedLockEngine.RegisterAsync(string name, DistributedLockInfo lockInfo, CancellationToken token)
            => lockInfo.IsExpired ? FalseTask.Task : ReportAcquisition(name, lockInfo, token);

        async Task<bool> IDistributedLockEngine.UnregisterAsync(string name, Guid owner, Guid version, CancellationToken token)
        {
            using (await WriteLock.AcquireAsync(token).ConfigureAwait(false))
                if (acquiredLocks.TryGetValue(name, out var existingLock) && existingLock.Owner == owner && existingLock.Version == version)
                    acquiredLocks = acquiredLocks.Remove(name);
                else
                    return false;
            //log entry can be added only out of write-lock scope
            await AppendAsync(new ReleaseLockCommand(name, Term), token).ConfigureAwait(false);
            return true;
        }

        async Task IDistributedLockEngine.UnregisterAsync(string name, CancellationToken token)
        {
            using (await WriteLock.AcquireAsync(token).ConfigureAwait(false))
                if (acquiredLocks.ContainsKey(name))
                    acquiredLocks = acquiredLocks.Remove(name);
                else
                    return;
            await AppendAsync(new ReleaseLockCommand(name, Term), token).ConfigureAwait(false);
        }

        bool IDistributedLockEngine.IsRegistered(string lockName, Guid version)
            => acquiredLocks.TryGetValue(lockName, out var lockInfo) && lockInfo.Version == version && lockInfo.Owner == NodeId;

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

        private async ValueTask SaveLockAsync(string lockName, DistributedLockInfo lockInfo)
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
            var builder = ImmutableDictionary.CreateBuilder<string, DistributedLockInfo>(acquiredLocks.KeyComparer);
            //restore lock state from file system
            const int fileBuffer = 1024;
            using var buffer = new ArrayRental<byte>(fileBuffer);
            foreach (var lockFile in lockPersistentStateStorage.EnumerateFiles())
                using (var fs = new FileStream(lockFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, true))
                {
                    var state = await fs.ReadAsync<DistributedLockInfo>(buffer.Memory, token).ConfigureAwait(false);
                    builder.Add(FileNameToLockName(lockFile.Name), state);
                }
            acquiredLocks = builder.ToImmutable();
        }
    }
}