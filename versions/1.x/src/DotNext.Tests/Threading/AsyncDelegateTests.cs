using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
    public sealed class AsyncDelegateTests : Assert
    {
        private sealed class Accumulator
        {
            private int counter;

            internal int Counter => counter;

            internal void IncBy1() => counter.Add(1);

            internal void IncBy3() => counter.Add(3);

            internal void IncBy5() => counter.Add(5);

            internal void Throw() => throw new Exception();
        }

        [Fact]
        public static async Task InvokeActionAsync()
        {
            var acc = new Accumulator();
            Action action = acc.IncBy1;
            action += acc.IncBy3;
            action += acc.IncBy5;
            await action.InvokeAsync();
            Equal(9, acc.Counter);
        }

        [Fact]
        public static async Task InvokeActionAsyncFailure()
        {
            var acc = new Accumulator();
            Action action = acc.IncBy1;
            action += acc.Throw;
            action += acc.IncBy3;
            await ThrowsAsync<AggregateException>(async () => await action.InvokeAsync());
            await ThrowsAsync<AggregateException>(action.InvokeAsync().AsTask);
        }
    }
}
