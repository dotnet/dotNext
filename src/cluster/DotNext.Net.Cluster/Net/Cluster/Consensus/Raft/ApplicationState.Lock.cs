using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MemoryMarshal = System.Runtime.InteropServices.MemoryMarshal;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Buffers;
    using DistributedServices;
    using Threading;
    using static Threading.Tasks.Synchronization;
    using static IO.StreamExtensions;
    using TrueTask = Threading.Tasks.CompletedTask<bool, Generic.BooleanConst.True>;

    public partial class ApplicationState : IDistributedLockEngine
    {
        //each file in directory contains description of distributed lock
        //file name format: <lockName>.<hash>
        //hash is needed to beat case sensivity problem
        //Its format described by LockState value type
        //If file exists then it means that the lock is acquired 
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

        private sealed class WaitNode : TaskCompletionSource<bool>
        {
            internal readonly Guid Owner;

            internal WaitNode(in Guid owner)
                : base(TaskCreationOptions.RunContinuationsAsynchronously)
            {
                Owner = owner;
            }
        }

        private readonly DirectoryInfo lockPersistentStateStorage;
        private readonly ConcurrentDictionary<string, WaitNode> lockAcquisitions;
        private readonly AsyncEventSource releaseEventSource = new AsyncEventSource();

        private async ValueTask AppendLockCommandAsync(LogEntry entry)
        {
            var bytes = await entry.ReadAsync(sizeof(LockCommand)).ConfigureAwait(false);
            switch(MemoryMarshal.Read<LockCommand>(bytes.Span))
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

        Task<bool> IDistributedLockEngine.WaitForAcquisitionAsync(string lockName, TimeSpan timeout, CancellationToken token)
        {
            var newNode = new WaitNode(NodeId);
            var currentNode = lockAcquisitions.GetOrAdd(lockName, newNode);
            //the lock was acquired previously so just wait
            //otherwise the lock was not acquired so return immediately 
            return ReferenceEquals(newNode, currentNode) ?
                TrueTask.Task :
                currentNode.Task.WaitAsync(timeout, token);
        }

        Task IDistributedLockEngine.CollectGarbage(CancellationToken token) => Task.CompletedTask;

        async Task IDistributedLockEngine.RestoreAsync(CancellationToken token)
        {
            //restore lock state from file system
            const int fileBuffer = 1024;
            using var buffer = new ArrayRental<byte>(fileBuffer);
            foreach(var lockFile in lockPersistentStateStorage.EnumerateFiles())
                await using(var fs = new FileStream(lockFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, fileBuffer, true))
                {
                    var state = await fs.ReadAsync<DistributedLockInfo>(buffer.Memory, token).ConfigureAwait(false);
                    lockAcquisitions[Path.GetFileNameWithoutExtension(lockFile.Name)] = new WaitNode(state.Owner);
                }
        }
    }
}