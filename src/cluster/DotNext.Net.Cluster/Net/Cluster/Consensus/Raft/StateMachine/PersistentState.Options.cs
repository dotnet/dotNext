using System.Numerics;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Buffers;

partial class PersistentState
{
    [StructLayout(LayoutKind.Auto)]
    public class Options
    {
        private readonly int pageSize = Environment.SystemPageSize;
        private readonly int concurrencyLevel = Environment.ProcessorCount * 2 + 1;

        public int PageSize
        {
            get => pageSize;
            init => pageSize = value > 0 ? (int)BitOperations.RoundUpToPowerOf2((uint)value) : throw new ArgumentOutOfRangeException(nameof(value));
        }

        public int ConcurrencyLevel
        {
            get => concurrencyLevel;
            init => concurrencyLevel = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }

        public MemoryAllocator<byte>? Allocator
        {
            get;
            init;
        }
    }
}