using System.IO;
using System.Threading.Tasks;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Buffers;
    using IO;

    public partial class PersistentState : IClusterConfigurationStorage
    {
        private const string MembershipStorageFileName = "members.list";
        private readonly FileStream memberListStorage;

        /// <summary>
        /// Gets or sets the configuration change tracker.
        /// </summary>
        public IClusterConfigurationStorage.IMembershipChangeHandler? MembershipChangeHandler { get; set; }

        private static async ValueTask UpdateMembershipAsync(LogEntry entry, bool addMember, IClusterConfigurationStorage.IMembershipChangeHandler handler, FileStream memberListStorage, MemoryAllocator<byte>? allocator)
        {
            Debug.Assert(entry.CommandId == (addMember ? IRaftLogEntry.AddServerCommandId : IRaftLogEntry.RemoveServerCommandId));

            using var outputBuffer = new PooledBufferWriter<byte>(allocator, memberListStorage.Length.Truncate());
            if (entry.TryGetMemory(out var address))
            {
                await handler.AddNodeAsync(address, outputBuffer).ConfigureAwait(false);
            }
            else
            {
                using var addressBuffer = await entry.ToMemoryAsync(allocator).ConfigureAwait(false);
                await handler.AddNodeAsync(addressBuffer.Memory, outputBuffer).ConfigureAwait(false);
            }

            // truncate and rewrite file
            memberListStorage.SetLength(outputBuffer.WrittenCount);
            await memberListStorage.WriteAsync(outputBuffer.WrittenMemory).ConfigureAwait(false);
            await memberListStorage.FlushAsync().ConfigureAwait(false);
        }
    }
}