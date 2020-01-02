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
    
    public partial class ApplicationState : IDistributedLockEngine
    {
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
                await writer.WriteAsync(name, context, null, token).ConfigureAwait(false);
                await writer.WriteAsync(owner, token).ConfigureAwait(false);
                await writer.WriteAsync(id, token).ConfigureAwait(false);
                await writer.WriteAsync(leaseTime, token).ConfigureAwait(false);
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
                case LockCommand.Acquire:
                    break;
            }
        }

        AsyncEventListener IDistributedLockEngine.CreateReleaseLockListener(CancellationToken token) => new AsyncEventListener(releaseEventSource, token);

        AsyncEventListener IDistributedLockEngine.CreateAcquireLockListener(CancellationToken token) => new AsyncEventListener(acquireEventSource, token);

        Task IDistributedLockEngine.CollectGarbage(CancellationToken token) => Task.CompletedTask;
        
        async Task<bool> IDistributedLockEngine.TryAcquireAsync(string name, DistributedLockInfo lockInfo, CancellationToken token)
        {
            if(lockInfo.IsExpired)
                return false;
            var updatedLockInfo = acquiredLocks.AddOrUpdate(name, lockInfo, lockInfo.Update);
            if(lockInfo.Id != updatedLockInfo.Id)
                return false;
            await AppendAsync(new AcquireLockCommand(name, lockInfo, Term), token).ConfigureAwait(false);
            return true;
        }

        bool IDistributedLockEngine.IsAcquired(string lockName, Guid version)
            => acquiredLocks.TryGetValue(lockName, out var lockInfo) && lockInfo.Id == version;
        
        private static string FileNameToLockName(string fileName)
            => fileName.FromBase64(Encoding.UTF8);
        
        private static string LockNameToFileName(string lockName)
            => lockName.ToBase64(Encoding.UTF8);

        async Task IDistributedLockEngine.RestoreAsync(CancellationToken token)
        {
            //restore lock state from file system
            const int fileBuffer = 1024;
            using var buffer = new ArrayRental<byte>(fileBuffer);
            foreach(var lockFile in lockPersistentStateStorage.EnumerateFiles())
                using(var fs = new FileStream(lockFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, fileBuffer, true))
                {
                    var state = await fs.ReadAsync<DistributedLockInfo>(buffer.Memory, token).ConfigureAwait(false);
                    acquiredLocks[FileNameToLockName(lockFile.Name)] = state;
                }
        }
    }
}