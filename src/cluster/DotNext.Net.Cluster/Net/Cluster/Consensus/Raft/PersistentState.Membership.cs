using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Globalization.CultureInfo;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Buffers;
    using IO;
    using Membership;

    public partial class PersistentState : IClusterConfigurationStorage
    {
        private const string MembershipStorageFileName = "members.list";

        /*
            Snapshot file format:
            8 bytes = running index
            sizeof(ClusterMemberId) = cluster member id
            4 bytes (N) = the length of the address (LE)
            octet stream of length N = the address
            ... zero or more repetitions
         */
        private sealed class MembershipStateMachine : Dictionary<ClusterMemberId, MemoryOwner<byte>>, IAsyncEnumerable<KeyValuePair<ClusterMemberId, ReadOnlyMemory<byte>>>, ISupplier<MemoryOwner<byte>>, IDisposable
        {
            private sealed class AsyncEnumerator : IAsyncEnumerator<KeyValuePair<ClusterMemberId, ReadOnlyMemory<byte>>>
            {
                private readonly CancellationToken token;
                private readonly IEnumerator<KeyValuePair<ClusterMemberId, MemoryOwner<byte>>> enumerator;

                internal AsyncEnumerator(IReadOnlyDictionary<ClusterMemberId, MemoryOwner<byte>> cache, CancellationToken token)
                {
                    enumerator = cache.GetEnumerator();
                    this.token = token;
                }

                public ValueTask<bool> MoveNextAsync() => new(!token.IsCancellationRequested && enumerator.MoveNext());

                public KeyValuePair<ClusterMemberId, ReadOnlyMemory<byte>> Current
                {
                    get
                    {
                        var pair = enumerator.Current;
                        return new(pair.Key, pair.Value.Memory);
                    }
                }

                public ValueTask DisposeAsync()
                {
                    var result = new ValueTask();
                    try
                    {
                        enumerator.Dispose();
                    }
                    catch (Exception e)
                    {
#if NETSTANDARD2_1
                        result = new(Task.FromException(e));
#else
                        result = ValueTask.FromException(e);
#endif
                    }

                    return result;
                }
            }

            private static readonly string[] TimestampFormats = { "O" };
            private static Encoding DefaultEncoding => Encoding.UTF8;

            private readonly MemoryAllocator<byte>? allocator;

            // the variable is needed because we need to skip all subsequent membership commands up to this index
            internal DateTimeOffset RunningTimestamp;

            internal MembershipStateMachine(MemoryAllocator<byte>? allocator)
                : base(15)
            {
                this.allocator = allocator;
                RunningTimestamp = DateTimeOffset.MinValue;
            }

            IAsyncEnumerator<KeyValuePair<ClusterMemberId, ReadOnlyMemory<byte>>> IAsyncEnumerable<KeyValuePair<ClusterMemberId, ReadOnlyMemory<byte>>>.GetAsyncEnumerator(CancellationToken token)
                => new AsyncEnumerator(this, token);

            internal MemoryOwner<byte> Bufferize()
            {
                if (Count == 0)
                    return default;

                using var buffer = new PooledBufferWriter<byte>(allocator, 512);
                Span<byte> memberIdBuffer = stackalloc byte[ClusterMemberId.Size];

                // serialize running timestamp
                buffer.WriteDateTimeOffset(RunningTimestamp, LengthFormat.PlainLittleEndian, DefaultEncoding, TimestampFormats[0], InvariantCulture);

                foreach (var (id, address) in this)
                {
                    // serialize member id
                    id.WriteTo(memberIdBuffer);
                    buffer.Write(memberIdBuffer);

                    // serialize address
                    buffer.WriteInt32(address.Length, true);
                    buffer.Write(address.Memory.Span);
                }

                return buffer.DetachBuffer();
            }

            MemoryOwner<byte> ISupplier<MemoryOwner<byte>>.Invoke() => Bufferize();

            // restores configuration from the file
            internal async ValueTask DeserializeAsync(Stream input, CancellationToken token = default)
            {
                input.Position = 0L;
                using var buffer = allocator.Invoke(64, false);

                // deserialize running timestamp
                RunningTimestamp = await input.ReadDateTimeOffsetAsync(LengthFormat.PlainLittleEndian, DefaultEncoding, TimestampFormats, buffer.Memory, DateTimeStyles.RoundtripKind, CultureInfo.InvariantCulture, token).ConfigureAwait(false);

                while (input.Position < input.Length)
                {
                    // deserialize member id
                    var memberIdBuffer = buffer.Memory.Slice(0, ClusterMemberId.Size);
                    await input.ReadAsync(memberIdBuffer, token).ConfigureAwait(false);
                    var id = new ClusterMemberId(memberIdBuffer.Span);

                    // deserialize address
                    var address = await input.ReadBlockAsync(LengthFormat.PlainLittleEndian, buffer.Memory, allocator, token).ConfigureAwait(false);

                    if (!TryAdd(id, address))
                        address.Dispose();
                }
            }

            internal async ValueTask SerializeAsync(Stream output, CancellationToken token = default)
            {
                output.SetLength(0L);
                using var buffer = allocator.Invoke(64, false);

                // serialize running timestamp
                await output.WriteDateTimeOffsetAsync(RunningTimestamp, LengthFormat.PlainLittleEndian, DefaultEncoding, buffer.Memory, TimestampFormats[0], CultureInfo.InvariantCulture, token).ConfigureAwait(false);

                foreach (var (id, address) in this)
                {
                    // serialize cluster member id
                    var memberIdBuffer = buffer.Memory.Slice(0, ClusterMemberId.Size);
                    id.WriteTo(memberIdBuffer.Span);
                    await output.WriteAsync(memberIdBuffer, token).ConfigureAwait(false);

                    // serialize address
                    await output.WriteBlockAsync(address.Memory, LengthFormat.PlainLittleEndian, buffer.Memory, token).ConfigureAwait(false);
                }
            }

            internal void Reload(ReadOnlyMemory<byte> configuration)
            {
                // cleanup existing entries
                Clear();

                var reader = IAsyncBinaryReader.Create(configuration);
                Span<byte> memberIdBuffer = stackalloc byte[ClusterMemberId.Size];

                // deserialize running timestamp
                RunningTimestamp = reader.ReadDateTimeOffset(LengthFormat.PlainLittleEndian, DefaultEncoding, TimestampFormats, DateTimeStyles.RoundtripKind, CultureInfo.InvariantCulture);

                while (!reader.RemainingSequence.IsEmpty)
                {
                    // deserialize cluster id
                    reader.Read(memberIdBuffer);
                    var id = new ClusterMemberId(memberIdBuffer);

                    var address = reader.Read(LengthFormat.PlainLittleEndian, allocator);

                    if (!TryAdd(id, address))
                        address.Dispose();
                }
            }

            internal new void Clear()
            {
                foreach (var address in Values)
                    address.Dispose();

                base.Clear();
            }

            private void Dispose(bool disposing)
            {
                if (disposing)
                    Clear();
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            ~MembershipStateMachine() => Dispose(false);
        }

        // membership tracking should be performed in the following places:
        // 1. Applying committed entry
        // 2. Read of snapshot
        // 3. Installallation of snapshot
        // 4. Snapshot construction even if custom SnapshotBuilder is not supplied
        private readonly FileStream membershipStorage;
        private readonly MembershipStateMachine membershipInterpreter;

        /// <summary>
        /// Gets or sets a configuration tracker.
        /// </summary>
        public IClusterConfigurationStorage.IConfigurationInterpreter? ConfigurationInterpreter { get; set; }

        private async ValueTask AddMemberAsync(LogEntry entry)
        {
            Debug.Assert(entry.CommandId == IRaftLogEntry.AddMemberCommandId);
            Debug.Assert(entry.Timestamp > membershipInterpreter.RunningTimestamp);

            var (id, address) = await entry.TransformAsync<(ClusterMemberId, MemoryOwner<byte>), AddMemberLogEntry.Deserializer>(new(bufferManager.BufferAllocator), CancellationToken.None).ConfigureAwait(false);

            // interpret a new member
            membershipInterpreter.RunningTimestamp = entry.Timestamp;
            if (membershipInterpreter.TryAdd(id, address))
            {
                var interpreter = ConfigurationInterpreter;
                if (interpreter is not null)
                    await interpreter.AddMemberAsync(id, address.Memory).ConfigureAwait(false);
            }
            else
            {
                address.Dispose();
            }
        }

        private async ValueTask RemoveMemberAsync(LogEntry entry)
        {
            Debug.Assert(entry.CommandId == IRaftLogEntry.RemoveMemberCommandId);
            Debug.Assert(entry.Timestamp > membershipInterpreter.RunningTimestamp);

            var id = await entry.TransformAsync<ClusterMemberId, RemoveMemberLogEntry.Deserializer>(new(bufferManager.BufferAllocator), CancellationToken.None).ConfigureAwait(false);

            // intepret removal of existing member
            membershipInterpreter.RunningTimestamp = entry.Timestamp;
            if (membershipInterpreter.Remove(id, out var address))
                address.Dispose();

            var interpreter = ConfigurationInterpreter;
            if (interpreter is not null)
                await interpreter.RemoveMemberAsync(id).ConfigureAwait(false);
        }

        private async ValueTask DumpConfigurationAsync()
        {
            await membershipInterpreter.SerializeAsync(membershipStorage).ConfigureAwait(false);
            await membershipStorage.FlushAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Restores information about all cluster members.
        /// </summary>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous result.</returns>
        public async ValueTask LoadConfigurationAsync(CancellationToken token = default)
        {
            if (membershipStorage.Length > 0 && membershipInterpreter.Count == 0)
                await membershipInterpreter.DeserializeAsync(membershipStorage, token).ConfigureAwait(false);

            var handler = ConfigurationInterpreter;
            if (handler is not null)
                await handler.RefreshAsync(membershipInterpreter, token).ConfigureAwait(false);
        }
    }
}