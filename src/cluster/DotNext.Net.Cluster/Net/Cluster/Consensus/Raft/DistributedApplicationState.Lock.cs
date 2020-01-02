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
    using FalseTask = Threading.Tasks.CompletedTask<bool, Generic.BooleanConst.False>;

    public partial class DistributedApplicationState : IDistributedLockEngine
    {
        private const int BufferSize = 1024;
        //each file in directory contains description of distributed lock
        //file name is base64 encoded lock name to avoid case sensitivity issues 
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
                id = lockInfo.Id;
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
                    Id = await entry.ReadAsync<Guid>().ConfigureAwait(false),
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
                    break;
            }
        }

        AsyncEventListener IDistributedLockEngine.CreateReleaseLockListener(CancellationToken token) => new AsyncEventListener(releaseEventSource, token);

        AsyncEventListener IDistributedLockEngine.CreateAcquireLockListener(CancellationToken token) => new AsyncEventListener(acquireEventSource, token);

        Task IDistributedLockEngine.CollectGarbage(CancellationToken token) => Task.CompletedTask;

        private async Task<bool> ReportAcquisition(string name, DistributedLockInfo lockInfo, CancellationToken token)
        {
            var updatedLockInfo = acquiredLocks.AddOrUpdate(name, lockInfo, lockInfo.Update);
            if (lockInfo.Id != updatedLockInfo.Id)
                return false;
            await AppendAsync(new AcquireLockCommand(name, lockInfo, Term), token).ConfigureAwait(false);
            return true;
        }

        Task<bool> IDistributedLockEngine.PrepareAcquisitionAsync(string name, DistributedLockInfo lockInfo, CancellationToken token)
            => lockInfo.IsExpired ? FalseTask.Task : ReportAcquisition(name, lockInfo, token);

        bool IDistributedLockEngine.IsAcquired(string lockName, Guid version)
            => acquiredLocks.TryGetValue(lockName, out var lockInfo) && lockInfo.Id == version;

        private static string FileNameToLockName(string fileName) => Uri.EscapeDataString(fileName);

        private static string LockNameToFileName(string lockName) => Uri.UnescapeDataString(lockName);

        private async ValueTask SaveLockAsync(string lockName, DistributedLockInfo lockInfo)
        {
            lockName = Path.Combine(lockPersistentStateStorage.FullName, LockNameToFileName(lockName));
            using var lockFile = new FileStream(lockName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None, BufferSize, true);
            //lockInfo has fixed size so no need to truncate file stream
            await lockFile.WriteAsync(lockInfo).ConfigureAwait(false);
            await lockFile.FlushAsync().ConfigureAwait(false);
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