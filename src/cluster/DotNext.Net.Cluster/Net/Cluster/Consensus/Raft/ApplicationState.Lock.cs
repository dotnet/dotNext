using System;
using System.IO.MemoryMappedFiles;
using System.Threading.Tasks;
using MemoryMarshal = System.Runtime.InteropServices.MemoryMarshal;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using DistributedServices;

    public partial class ApplicationState : IDistributedLockEngine
    {
        /// <summary>
        /// Gets the prefix of log entry that represents
        /// distributed lock command.
        /// </summary>
        [CLSCompliant(false)]
        protected const uint LockCommandId = 0xedb88320;

        /// <summary>
        /// Represents the command associated with
        /// distributed lock.
        /// </summary>
        protected enum LockCommand : short
        {
            Nop = 0,
            Release,
            Acquire
        }

        private readonly MemoryMappedFile lockFile;
        private readonly MemoryMappedViewAccessor lockView;

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
    }
}