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
    using static Threading.Tasks.Synchronization;
    using TimeoutTracker = Threading.Timeout;
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

        private struct LockState
        {
            private DateTimeOffset creationTime;

            internal Guid Owner;
            internal DateTimeOffset CreationTime
            {
                get => creationTime;
                set => creationTime = value.ToUniversalTime();
            }

            internal TimeSpan LeaseTime;

            internal bool IsExpired
            {
                get
                {
                    var currentTime = DateTimeOffset.Now.ToUniversalTime();
                    return CreationTime + LeaseTime <= currentTime;
                }
            }           
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

        private readonly DirectoryInfo lockState;
        private readonly ConcurrentDictionary<string, WaitNode> waitNodes;

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

        Task<bool> IDistributedLockEngine.WaitForAcquisitionAsync(string lockName, TimeSpan timeout, CancellationToken token)
        {
            var newNode = new WaitNode(NodeId);
            var currentNode = waitNodes.GetOrAdd(lockName, newNode);
            //the lock was acquired previously so just wait
            //otherwise the lock was not acquired so return immediately 
            return ReferenceEquals(newNode, currentNode) ?
                TrueTask.Task :
                currentNode.Task.WaitAsync(timeout, token);
        }

        async Task IDistributedLockEngine.RestoreAsync(CancellationToken token)
        {
            //restore lock state from file system
            const int fileBuffer = 1024;
            using var buffer = new ArrayRental<byte>(fileBuffer);
            foreach(var lockFile in lockState.EnumerateFiles())
                await using(var fs = new FileStream(lockFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, fileBuffer, true))
                {
                    var state = await fs.ReadAsync<LockState>(buffer.Memory, token).ConfigureAwait(false);
                    waitNodes[Path.GetFileNameWithoutExtension(lockFile.Name)] = new WaitNode(state.Owner);
                }
        }
    }
}