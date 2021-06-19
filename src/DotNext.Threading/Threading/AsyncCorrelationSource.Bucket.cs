using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotNext.Threading
{
    public partial class AsyncCorrelationSource<TKey, TValue>
    {
        internal sealed class Slot : TaskCompletionSource<TValue>
        {
            private readonly TKey expected;
            private readonly IEqualityComparer<TKey> comparer;

            internal Slot(TKey value, IEqualityComparer<TKey> comparer)
                : base(TaskCreationOptions.RunContinuationsAsynchronously)
            {
                expected = value;
                this.comparer = comparer;
            }

            internal bool TrySetResult(TKey actual, TValue value)
                => comparer.Equals(expected, actual) && TrySetResult(value);
        }

        private sealed class Bucket : LinkedList<Slot>
        {
        }
    }
}