using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DotNext.Threading.Tasks
{
    internal abstract class ValueTaskCompletionSource<R>
    {
        private AsyncValueTaskMethodBuilder<ValueTask<R>> builder;
        private AtomicBoolean completed;

        private protected ValueTaskCompletionSource()
        {
            builder = AsyncValueTaskMethodBuilder<ValueTask<R>>.Create();
            completed = new AtomicBoolean(false);
        }

        private protected void Complete(ValueTask<R> task)
        {
            if (completed.FalseToTrue())
                builder.SetResult(task);
        }

        internal ValueTask<ValueTask<R>> Task => builder.Task;
    }

    internal abstract class ValueTaskCompletionSource
    {
        private AsyncValueTaskMethodBuilder<ValueTask> builder;
        private AtomicBoolean completed;

        private protected ValueTaskCompletionSource()
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

    internal class ValueTaskCompletionSource2<R> : ValueTaskCompletionSource<R>
    {
        private readonly ValueTask<R> first, second;

        internal ValueTaskCompletionSource2(ValueTask<R> first, ValueTask<R> second)
        {
            this.first = first;
            this.second = second;
        }

        internal void CompleteFirst() => Complete(first);

        internal void CompleteSecond() => Complete(second);
    }

    internal class ValueTaskCompletionSource2 : ValueTaskCompletionSource
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

    internal class ValueTaskCompletionSource3<R> : ValueTaskCompletionSource2<R>
    {
        private readonly ValueTask<R> third;

        internal ValueTaskCompletionSource3(ValueTask<R> first, ValueTask<R> second, ValueTask<R> third)
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

    internal class ValueTaskCompletionSource4<R> : ValueTaskCompletionSource3<R>
    {
        private readonly ValueTask<R> fourth;

        internal ValueTaskCompletionSource4(ValueTask<R> first, ValueTask<R> second, ValueTask<R> third, ValueTask<R> fourth)
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

    internal class ValueTaskCompletionSource5<R> : ValueTaskCompletionSource4<R>
    {
        private readonly ValueTask<R> fifth;

        internal ValueTaskCompletionSource5(ValueTask<R> first, ValueTask<R> second, ValueTask<R> third, ValueTask<R> fourth, ValueTask<R> fifth)
            : base(first, second, third, fourth)
        {
            this.fifth = fifth;
        }

        internal void CompleteFifth() => Complete(fifth);
    }
}