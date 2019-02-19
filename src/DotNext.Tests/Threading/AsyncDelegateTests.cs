using System;
using Xunit;

namespace DotNext.Threading
{
    public sealed class AsyncDelegateTests: Assert
    {
        private sealed class Accumulator
        {
            private int counter;

            internal int Counter => counter;

            internal void IncBy1() => counter.Add(1);

            internal void IncBy3() => counter.Add(3);

            internal void IncBy5() => counter.Add(5);
        }

        [Fact]
        public void InvokeEventHandlerTest()
        {
            var acc = new Accumulator();
            Action action = acc.IncBy1;
            action += acc.IncBy3;
            action += acc.IncBy5;
            var task = action.InvokeAsync();
            task.Wait();
            Equal(9, acc.Counter);
        }
    }
}
