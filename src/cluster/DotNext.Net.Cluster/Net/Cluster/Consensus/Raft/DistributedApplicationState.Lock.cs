using System;
using System.Collections.Concurrent;
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

        [StructLayout(LayoutKind.Auto)]
        private readonly struct AcquireLockCommand : IWriteOnlyLogEntry
        {
            private readonly long term;
            private readonly DateTimeOffset timestamp;
            private readonly ReadOnlyMemory<char> name;
            //it is struct for faster serialization with writer
            private readonly Guid owner, id;
            private readonly TimeSpan leaseTime;

            internal AcquireLockCommand(string lockName, DistributedLockInfo lockInfo, long term)
            {
                this.term = term;
                timestamp = lockInfo.CreationTime;
                name = lockName.AsMemory();
                owner = lockInfo.Owner;
                id = lockInfo.Version;
                leaseTime = lockInfo.LeaseTime;
            }

            long IRaftLogEntry.Term => term;

            DateTimeOffset ILogEntry.Timestamp => timestamp;

            async ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            {
                await writer.WriteAsync(LockCommand.Acquire, token).ConfigureAwait(false);
                var context = new EncodingContext(Encoding.Unicode, true);
                await writer.WriteAsync(name, context, StringLengthEncoding.Plain, token).ConfigureAwait(false);
                await writer.WriteAsync(owner, token).ConfigureAwait(false);
                await writer.WriteAsync(id, token).ConfigureAwait(false);
                await writer.WriteAsync(leaseTime, token).ConfigureAwait(false);
            }

            internal static async ValueTask<(string, DistributedLockInfo)> ReadAsync(LogEntry entry)
            {
                var context = new DecodingContext(Encoding.Unicode, true);
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

        private readonly ConcurrentDictionary<string, DistributedLockInfo> acquiredLocks;
        private readonly DirectoryInfo lockPersistentStateStorage;
        private readonly AsyncEventSource releaseEventSource = new AsyncEventSource();
        private readonly AsyncEventSource acquireEventSource = new AsyncEventSource();

        private async ValueTask AppendLockCommandAsync(LogEntry entry)
        {
            var command = await entry.ReadAsync<LockCommand>().ConfigureAwait(false);
            switch(command)
            {
                default:
                    throw new InvalidOperationException();
                case LockCommand.Nop:
                    break;
                case LockCommand.Acquire:   //confirm acquisition
                    var (lockName, lockInfo) = await AcquireLockCommand.ReadAsync(entry).ConfigureAwait(false);
                    acquiredLocks[lockName] = lockInfo;
                    //save lock state to file
                    await SaveLockAsync(lockName, lockInfo).ConfigureAwait(false);
                    acquireEventSource.Resume();
                    break;
                case LockCommand.Release:
                    releaseEventSource.Resume();
                    break;
            }
        }

        AsyncEventListener IDistributedLockEngine.CreateReleaseLockListener(CancellationToken token) => new AsyncEventListener(releaseEventSource, token);

        AsyncEventListener IDistributedLockEngine.CreateAcquireLockListener(CancellationToken token) => new AsyncEventListener(acquireEventSource, token);

        Task IDistributedLockEngine.CollectGarbage(CancellationToken token) => Task.CompletedTask;

        private async ValueTask<bool> ReportAcquisition(string name, DistributedLockInfo lockInfo, CancellationToken token)
        {
            var updatedLockInfo = acquiredLocks.AddOrUpdate(name, lockInfo, lockInfo.Update);
            if (lockInfo.Version != updatedLockInfo.Version)
                return false;
            await AppendAsync(new AcquireLockCommand(name, lockInfo, Term), token).ConfigureAwait(false);
            return true;
        }

        ValueTask<bool> IDistributedLockEngine.PrepareAcquisitionAsync(string name, DistributedLockInfo lockInfo, CancellationToken token)
            => lockInfo.IsExpired ? new ValueTask<bool>(false) : ReportAcquisition(name, lockInfo, token);

        bool IDistributedLockEngine.IsAcquired(string lockName, Guid version)
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

        private async ValueTask SaveLockAsync(string lockName, DistributedLockInfo lockInfo)
        {
            lockName = Path.Combine(lockPersistentStateStorage.FullName, LockNameToFileName(lockName));
            using var lockFile = new FileStream(lockName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, BufferSize, true);
            //lockInfo has fixed size so no need to truncate file stream
            await lockFile.WriteAsync(lockInfo).ConfigureAwait(false);
            await lockFile.FlushAsync().ConfigureAwait(false);
        }

        void IDistributedLockEngine.ValidateLockName(string name)
        {
            if(name.Length > MaxLockNameLength)
                throw new ArgumentOutOfRangeException(nameof(name), ExceptionMessages.LockNameTooLong);
        }

        async Task IDistributedLockEngine.RestoreAsync(CancellationToken token)
        {
            //restore lock state from file system
            const int fileBuffer = 1024;
            using var buffer = new ArrayRental<byte>(fileBuffer);
            foreach(var lockFile in lockPersistentStateStorage.EnumerateFiles())
                using(var fs = new FileStream(lockFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, true))
                {
                    var state = await fs.ReadAsync<DistributedLockInfo>(buffer.Memory, token).ConfigureAwait(false);
                    acquiredLocks[FileNameToLockName(lockFile.Name)] = state;
                }
        }
    }
}