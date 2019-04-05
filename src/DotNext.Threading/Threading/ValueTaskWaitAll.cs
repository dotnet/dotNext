using System.Collections.Generic;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace DotNext.Threading
{
    internal struct ValueTaskWaitAll<T>: IAsyncStateMachine
        where T : struct, IEnumerator<ConfiguredValueTaskAwaitable>
    {
        private readonly long length;
        private readonly T awaiters;
        private long counter;
        private readonly AsyncValueTaskMethodBuilder builder;

        internal ValueTaskWaitAll(T awaiters, long length)
        {
            builder = AsyncValueTaskMethodBuilder.Create();
            this.length = length;
            counter = 0L;
            this.awaiters = awaiters;
        }

        void IAsyncStateMachine.MoveNext()
        {
            if(counter.IncrementAndGet() == length)
            {
                while(awaiters.MoveNext())
                    try
                    {
                        awaiters.Current.GetResult();
                    }
                    catch(Exception e)
                    {
                        awaiters.Dispose();
                        builder.SetException(e);
                        return;
                    }
                awaiters.Dispose();
                builder.SetResult();
            }
        }

        internal ValueTask Start()
        {
            builder.Start(ref this);
            return builder.Task;
        }

        void IAsyncStateMachine.SetStateMachine(IAsyncStateMachine stateMachine) => builder.SetStateMachine(stateMachine);
    }
}