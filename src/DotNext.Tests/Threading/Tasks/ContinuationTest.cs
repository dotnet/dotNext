using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Threading.Tasks
{
    using Generic;

    [ExcludeFromCodeCoverage]
    public sealed class ContinuationTest : Test
    {
        [Fact]
        public static async Task OnCompletedContinuation()
        {
            var t = Task.Delay(50);
            var t2 = await t.OnCompleted();
            Equal(t, t2);
            True(t.IsCompletedSuccessfully);
        }

        [Fact]
        public static async Task OnCompletedContinuation2()
        {
            var t = Task<int>.Factory.StartNew(() =>
            {
                Thread.Sleep(50);
                return 42;
            });
            var t2 = await t.OnCompleted();
            Equal(t, t2);
            True(t.IsCompletedSuccessfully);
            Equal(42, t2.Result);
        }

        [Fact]
        public static async Task OnFaulted()
        {
            var task = Task.FromException<int>(new Exception());
            Equal(int.MinValue, await task.OnFaulted<int, Int32Const.Min>());
            task = Task.FromResult(10);
            Equal(10, await task.OnFaulted<int, Int32Const.Min>());
        }

        [Fact]
        public static async Task OnFaultedOrCanceled()
        {
            var task = Task.FromException<int>(new Exception());
            Equal(int.MinValue, await task.OnFaultedOrCanceled<int, Int32Const.Min>());
            task = Task.FromResult(10);
            Equal(10, await task.OnFaultedOrCanceled<int, Int32Const.Min>());
            task = Task.FromCanceled<int>(new CancellationToken(true));
            Equal(int.MinValue, await task.OnFaultedOrCanceled<int, Int32Const.Min>());
        }

        [Fact]
        public static async Task OnCanceled()
        {
            var task = Task.FromException<int>(new ArithmeticException());
            await ThrowsAsync<ArithmeticException>(() => task.OnCanceled<int, Int32Const.Zero>());
            var source = new TaskCompletionSource<int>();
            task = source.Task.OnCanceled<int, Int32Const.Zero>();
            source.SetException(new ArithmeticException());
            await ThrowsAsync<ArithmeticException>(() => task);
        }

        [Fact]
        public static async Task OnFaultedConditional()
        {
            var task = Task.FromException<int>(new ArithmeticException());
            Predicate<AggregateException> filter = e => e.InnerException is ArithmeticException;
            task = task.OnFaulted<int, Int32Const.Min>(filter);
            Equal(int.MinValue, await task);
            var source = new TaskCompletionSource<int>();
            task = source.Task.OnFaulted<int, Int32Const.Min>(filter);
            source.SetException(new ArithmeticException());
            Equal(int.MinValue, await task);
            source = new TaskCompletionSource<int>();
            task = source.Task.OnFaulted<int, Int32Const.Min>(filter);
            source.SetException(new IndexOutOfRangeException());
            await ThrowsAsync<IndexOutOfRangeException>(() => task);
        }

        [Fact]
        public static async Task OnFaultedOrCanceledConditional()
        {
            var task = Task.FromCanceled<int>(new CancellationToken(true));
            Predicate<AggregateException> filter = e => e.InnerException is ArithmeticException;
            task = task.OnFaultedOrCanceled<int, Int32Const.Min>(filter);
            Equal(int.MinValue, await task);
            var source = new TaskCompletionSource<int>();
            task = source.Task.OnFaulted<int, Int32Const.Min>(filter);
            source.SetException(new ArithmeticException());
            Equal(int.MinValue, await task);
            source = new TaskCompletionSource<int>();
            task = source.Task.OnFaulted<int, Int32Const.Min>(filter);
            source.SetException(new IndexOutOfRangeException());
            await ThrowsAsync<IndexOutOfRangeException>(() => task);
        }
    }
}