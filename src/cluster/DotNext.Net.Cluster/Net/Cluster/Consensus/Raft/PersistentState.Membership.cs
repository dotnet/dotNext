using System;
using System.Buffers;
using System.IO;
using System.Threading;
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
        /// Gets or sets tracker of membership changes in the underlying storage.
        /// </summary>
        public Func<ReadOnlyMemory<byte>, bool, IBufferWriter<byte>, ValueTask>? MembershipTracker { get; set; }

        private static async ValueTask UpdateMembershipAsync(LogEntry entry, bool addMember, Func<ReadOnlyMemory<byte>, bool, IBufferWriter<byte>, ValueTask> tracker, FileStream memberListStorage, MemoryAllocator<byte>? allocator)
        {
            Debug.Assert(entry.CommandId == (addMember ? IRaftLogEntry.AddServerCommandId : IRaftLogEntry.RemoveServerCommandId));

            using var outputBuffer = new PooledBufferWriter<byte>(allocator, memberListStorage.Length.Truncate());
            if (entry.TryGetMemory(out var address))
            {
                await tracker(address, addMember, outputBuffer).ConfigureAwait(false);
            }
            else
            {
                using var addressBuffer = await entry.ToMemoryAsync(allocator).ConfigureAwait(false);
                await tracker(addressBuffer.Memory, addMember, outputBuffer).ConfigureAwait(false);
            }

            // truncate and rewrite file
            memberListStorage.SetLength(outputBuffer.WrittenCount);
            await memberListStorage.WriteAsync(outputBuffer.WrittenMemory).ConfigureAwait(false);
            await memberListStorage.FlushAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Loads information about all cluster members from the storage.
        /// </summary>
        /// <param name="loader">The reader of the configuration.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous result.</returns>
        public async ValueTask LoadConfigurationAsync(Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> loader, CancellationToken token = default)
        {
            memberListStorage.Position = 0;

            if (memberListStorage.Length == 0L)
            {
                await loader(ReadOnlyMemory<byte>.Empty, token).ConfigureAwait(false);
            }
            else
            {
                using var buffer = new PooledBufferWriter<byte>(bufferManager.BufferAllocator, memberListStorage.Length.Truncate());
                await memberListStorage.CopyToAsync(buffer, token: token).ConfigureAwait(false);
                await loader(buffer.WrittenMemory, token).ConfigureAwait(false);
            }
        }
    }
}