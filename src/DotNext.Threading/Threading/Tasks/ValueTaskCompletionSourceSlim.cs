using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DotNext.Threading.Tasks
{
    internal abstract class ValueTaskCompletionSourceSlim<TResult>
    {
        private AsyncValueTaskMethodBuilder<ValueTask<TResult>> builder;
        private AtomicBoolean completed;

        private protected ValueTaskCompletionSourceSlim()
        {
            builder = AsyncValueTaskMethodBuilder<ValueTask<TResult>>.Create();
            completed = new AtomicBoolean(false);
        }

        private protected void Complete(ValueTask<TResult> task)
        {
            if (completed.FalseToTrue())
                builder.SetResult(task);
        }

        internal ValueTask<ValueTask<TResult>> Task => builder.Task;
    }

    internal abstract class ValueTaskCompletionSourceSlim
    {
        private AsyncValueTaskMethodBuilder<ValueTask> builder;
        private AtomicBoolean completed;

        private protected ValueTaskCompletionSourceSlim()
        {
            builder = AsyncValueTaskMethodBuilder<ValueTask>.Create();
            completed = new AtomicBoolean(false);
        }

        private protected void Complete(ValueTask task)
        {
            if (completed.FalseToTrue())
                builder.SetResult(task);
        }

        internal ValueTask<ValueTask> Task => builder.Task;
    }

    internal class ValueTaskCompletionSource2<TResult> : ValueTaskCompletionSourceSlim<TResult>
    {
        private readonly ValueTask<TResult> first, second;

        internal ValueTaskCompletionSource2(ValueTask<TResult> first, ValueTask<TResult> second)
        {
            this.first = first;
            this.second = second;
        }

        internal void CompleteFirst() => Complete(first);

        internal void CompleteSecond() => Complete(second);
    }

    internal class ValueTaskCompletionSource2 : ValueTaskCompletionSourceSlim
    {
        private readonly ValueTask first, second;

        internal ValueTaskCompletionSource2(ValueTask first, ValueTask second)
        {
            this.first = first;
            this.second = second;
        }

        internal void CompleteFirst() => Complete(first);

        internal void CompleteSecond() => Complete(second);
    }

    internal class ValueTaskCompletionSource3 : ValueTaskCompletionSource2
    {
        private readonly ValueTask third;

        internal ValueTaskCompletionSource3(ValueTask first, ValueTask second, ValueTask third)
            : base(first, second)
        {
            this.third = third;
        }

        internal void CompleteThird() => Complete(third);
    }

    internal class ValueTaskCompletionSource3<TResult> : ValueTaskCompletionSource2<TResult>
    {
        private readonly ValueTask<TResult> third;

        internal ValueTaskCompletionSource3(ValueTask<TResult> first, ValueTask<TResult> second, ValueTask<TResult> third)
            : base(first, second)
        {
            this.third = third;
        }

        internal void CompleteThird() => Complete(third);
    }

    internal class ValueTaskCompletionSource4 : ValueTaskCompletionSource3
    {
        private readonly ValueTask fourth;

        internal ValueTaskCompletionSource4(ValueTask first, ValueTask second, ValueTask third, ValueTask fourth)
            : base(first, second, third)
        {
            this.fourth = fourth;
        }

        internal void CompleteFourth() => Complete(fourth);
    }

    internal class ValueTaskCompletionSource4<TResult> : ValueTaskCompletionSource3<TResult>
    {
        private readonly ValueTask<TResult> fourth;

        internal ValueTaskCompletionSource4(ValueTask<TResult> first, ValueTask<TResult> second, ValueTask<TResult> third, ValueTask<TResult> fourth)
            : base(first, second, third)
        {
            this.fourth = fourth;
        }

        internal void CompleteFourth() => Complete(fourth);
    }

    internal class ValueTaskCompletionSource5 : ValueTaskCompletionSource4
    {
        private readonly ValueTask fifth;

        internal ValueTaskCompletionSource5(ValueTask first, ValueTask second, ValueTask third, ValueTask fourth, ValueTask fifth)
            : base(first, second, third, fourth)
        {
            this.fifth = fifth;
        }

        internal void CompleteFifth() => Complete(fifth);
    }

    internal class ValueTaskCompletionSource5<TResult> : ValueTaskCompletionSource4<TResult>
    {
        private readonly ValueTask<TResult> fifth;

        internal ValueTaskCompletionSource5(ValueTask<TResult> first, ValueTask<TResult> second, ValueTask<TResult> third, ValueTask<TResult> fourth, ValueTask<TResult> fifth)
            : base(first, second, third, fourth)
        {
            this.fifth = fifth;
        }

        internal void CompleteFifth() => Complete(fifth);
    }
}