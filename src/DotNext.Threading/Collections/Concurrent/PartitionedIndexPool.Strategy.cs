namespace DotNext.Collections.Concurrent;

using Patterns;

partial struct PartitionedIndexPool
{
    /// <summary>
    /// Represents partitioning strategy.
    /// </summary>
    public abstract class PartitioningStrategy
    {
        private protected PartitioningStrategy()
        {
        }
        
        internal abstract int GetPartition();

        /// <summary>
        /// The partition selected by <see cref="PartitionedIndexPool.TryTake"/> method is chosen randomly.
        /// </summary>
        /// <remarks>
        /// This strategy is recommended if <see cref="PartitionedIndexPool.TryTake"/> and <see cref="PartitionedIndexPool.Return"/>
        /// are called withing async method because the managed thread can be changed during the method execution.
        /// </remarks>
        public static PartitioningStrategy Random => RandomStrategy.Instance;

        /// <summary>
        /// The partition selected by <see cref="PartitionedIndexPool.TryTake"/> method is chosen based on <see cref="Environment.CurrentManagedThreadId"/>.
        /// </summary>
        /// <remarks>
        /// This strategy is recommended if <see cref="PartitionedIndexPool.TryTake"/> and <see cref="PartitionedIndexPool.Return"/>
        /// are called withing synchronous method because the managed thread cannot be changed during the method execution.
        /// </remarks>
        public static PartitioningStrategy ManagedThreadId => ManagedThreadIdStrategy.Instance;
    }
    
    private sealed class RandomStrategy : PartitioningStrategy, ISingleton<RandomStrategy>
    {
        public static RandomStrategy Instance { get; } = new();

        private RandomStrategy()
        {
        }

        internal override int GetPartition() => System.Random.Shared.Next();
    }
    
    private sealed class ManagedThreadIdStrategy : PartitioningStrategy, ISingleton<ManagedThreadIdStrategy>
    {
        public static ManagedThreadIdStrategy Instance { get; } = new();

        private ManagedThreadIdStrategy()
        {
        }

        internal override int GetPartition() => Environment.CurrentManagedThreadId;
    }
}