using System;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    public partial class ApplicationState
    {
        /// <summary>
        /// Represents advanced configuration of persistent application state.
        /// </summary>
        public new class Options : PersistentState.Options
        {
            private const int DefaultMaxLockCount = 128;
            private int maxLockCount = DefaultMaxLockCount;

            /// <summary>
            /// Gets or sets the maximum count of distributed locks.
            /// </summary>
            public int MaxLockCount
            {
                get => maxLockCount;
                set => maxLockCount = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
            }
        }
    }
}