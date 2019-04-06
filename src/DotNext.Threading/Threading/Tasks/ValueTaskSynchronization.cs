using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DotNext.Threading.Tasks
{
    using Collections.Generic;

    public static class ValueTaskSynchronization
    {
        public static async ValueTask WhenAll(ValueTask task1, ValueTask task2)
        {
            await task1;
            await task2;
        }

        public static async ValueTask<(T1, T2)> WhenAll<T1, T2>(ValueTask<T1> task1, ValueTask<T2> task2) => (await task1, await task2);

        public static async ValueTask WhenAll(ValueTask task1, ValueTask task2, ValueTask task3)
        {
            await task1;
            await task2;
            await task3;
        }

        public static async ValueTask<(T1, T2, T3)> WhenAll<T1, T2, T3>(ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3) => (await task1, await task2, await task3);

        public static async ValueTask WhenAll(ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4)
        {
            await task1;
            await task2;
            await task3;
            await task4;
        }

        public static async ValueTask<(T1, T2, T3, T4)> WhenAll<T1, T2, T3, T4>(ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4) => (await task1, await task2, await task3, await task4);

        public static async ValueTask WhenAll(ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5)
        {
            await task1;
            await task2;
            await task3;
            await task4;
            await task5;
        }

        public static async ValueTask<(T1, T2, T3, T4, T5)> WhenAll<T1, T2, T3, T4, T5>(ValueTask<T1> task1, ValueTask<T2> task2, ValueTask<T3> task3, ValueTask<T4> task4, ValueTask<T5> task5) => (await task1, await task2, await task3, await task4, await task5);

        public static ValueTask<ValueTask> WhenAny(ValueTask task1, ValueTask task2)
        {
            if(task1.IsCompleted)
                return new ValueTask<ValueTask>(task1);
            else if(task2.IsCompleted)
                return new ValueTask<ValueTask>(task2);
            var whenAny = new ValueTaskCompletionSource2(task1, task2);
            task1.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteFirst);
            task2.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteSecond);
            return whenAny.Task;
        }

        public static ValueTask<ValueTask<R>> WhenAny<R>(ValueTask<R> task1, ValueTask<R> task2)
        {
            if(task1.IsCompleted)
                return new ValueTask<ValueTask<R>>(task1);
            else if(task2.IsCompleted)
                return new ValueTask<ValueTask<R>>(task2);
            var whenAny = new ValueTaskCompletionSource2<R>(task1, task2);
            task1.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteFirst);
            task2.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteSecond);
            return whenAny.Task;
        }

        public static ValueTask<ValueTask> WhenAny(ValueTask task1, ValueTask task2, ValueTask task3)
        {
            if(task1.IsCompleted)
                return new ValueTask<ValueTask>(task1);
            else if(task2.IsCompleted)
                return new ValueTask<ValueTask>(task2);
            else if(task3.IsCompleted)
                return new ValueTask<ValueTask>(task3);
            var whenAny = new ValueTaskCompletionSource3(task1, task2, task3);
            task1.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteFirst);
            task2.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteSecond);
            task3.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteThird);
            return whenAny.Task;
        }

        public static ValueTask<ValueTask<R>> WhenAny<R>(ValueTask<R> task1, ValueTask<R> task2, ValueTask<R> task3)
        {
            if(task1.IsCompleted)
                return new ValueTask<ValueTask<R>>(task1);
            else if(task2.IsCompleted)
                return new ValueTask<ValueTask<R>>(task2);
            else if(task3.IsCompleted)
                return new ValueTask<ValueTask<R>>(task3);
            var whenAny = new ValueTaskCompletionSource3<R>(task1, task2, task3);
            task1.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteFirst);
            task2.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteSecond);
            task3.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteThird);
            return whenAny.Task;
        }

        public static ValueTask<ValueTask> WhenAny(ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4)
        {
            if(task1.IsCompleted)
                return new ValueTask<ValueTask>(task1);
            else if(task2.IsCompleted)
                return new ValueTask<ValueTask>(task2);
            else if(task3.IsCompleted)
                return new ValueTask<ValueTask>(task3);
            else if(task4.IsCompleted)
                return new ValueTask<ValueTask>(task4);
            var whenAny = new ValueTaskCompletionSource4(task1, task2, task3, task4);
            task1.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteFirst);
            task2.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteSecond);
            task3.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteThird);
            task4.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteFourth);
            return whenAny.Task;
        }

        public static ValueTask<ValueTask<R>> WhenAny<R>(ValueTask<R> task1, ValueTask<R> task2, ValueTask<R> task3, ValueTask<R> task4)
        {
            if(task1.IsCompleted)
                return new ValueTask<ValueTask<R>>(task1);
            else if(task2.IsCompleted)
                return new ValueTask<ValueTask<R>>(task2);
            else if(task3.IsCompleted)
                return new ValueTask<ValueTask<R>>(task3);
            else if(task4.IsCompleted)
                return new ValueTask<ValueTask<R>>(task4);
            var whenAny = new ValueTaskCompletionSource4<R>(task1, task2, task3, task4);
            task1.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteFirst);
            task2.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteSecond);
            task3.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteThird);
            task4.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteFourth);
            return whenAny.Task;
        }

        public static ValueTask<ValueTask> WhenAny(ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5)
        {
            if(task1.IsCompleted)
                return new ValueTask<ValueTask>(task1);
            else if(task2.IsCompleted)
                return new ValueTask<ValueTask>(task2);
            else if(task3.IsCompleted)
                return new ValueTask<ValueTask>(task3);
            else if(task4.IsCompleted)
                return new ValueTask<ValueTask>(task4);
            else if(task5.IsCompleted)
                return new ValueTask<ValueTask>(task5);
            var whenAny = new ValueTaskCompletionSource5(task1, task2, task3, task4, task5);
            task1.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteFirst);
            task2.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteSecond);
            task3.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteThird);
            task4.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteFourth);
            task5.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteFifth);
            return whenAny.Task;
        }

        public static ValueTask<ValueTask<R>> WhenAny<R>(ValueTask<R> task1, ValueTask<R> task2, ValueTask<R> task3, ValueTask<R> task4, ValueTask<R> task5)
        {
            if(task1.IsCompleted)
                return new ValueTask<ValueTask<R>>(task1);
            else if(task2.IsCompleted)
                return new ValueTask<ValueTask<R>>(task2);
            else if(task3.IsCompleted)
                return new ValueTask<ValueTask<R>>(task3);
            else if(task4.IsCompleted)
                return new ValueTask<ValueTask<R>>(task4);
            else if(task5.IsCompleted)
                return new ValueTask<ValueTask<R>>(task5);
            var whenAny = new ValueTaskCompletionSource5<R>(task1, task2, task3, task4, task5);
            task1.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteFirst);
            task2.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteSecond);
            task3.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteThird);
            task4.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteFourth);
            task5.ConfigureAwait(false).GetAwaiter().OnCompleted(whenAny.CompleteFifth);
            return whenAny.Task;
        }
    }
}