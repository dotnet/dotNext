using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Buffers;

public partial class PersistentState
{
    internal interface IBufferManagerSettings
    {
        MemoryAllocator<T> GetMemoryAllocator<T>();

        bool UseCaching { get; }
    }
    
    [StructLayout(LayoutKind.Auto)]
    internal readonly struct BufferManager(IBufferManagerSettings settings)
    {
        public readonly MemoryAllocator<MemoryOwner<byte>> CacheAllocator = settings.GetMemoryAllocator<MemoryOwner<byte>>();
        public readonly MemoryAllocator<byte> BufferAllocator = settings.GetMemoryAllocator<byte>();
        public readonly bool IsCachingEnabled = settings.UseCaching;
    }
}