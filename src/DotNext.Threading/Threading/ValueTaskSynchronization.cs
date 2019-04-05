using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DotNext.Threading
{
    using Collections.Generic;

    public static class ValueTaskSynchronization
    {
        public static ValueTask WaitAll(this ValueTask task1, ValueTask task2)
        {
            var awaiter = new ValueTaskWaitAll<Pair<ValueTask>>(new Pair<ConfiguredValueTaskAwaitable> { 
                First = task1.ConfigureAwait(false), 
                Second = task2.ConfigureAwait(false) 
            }, Pair<ConfiguredTaskAwaitable>.Count);
            return awaiter.Start();
        }

        public static ValueTask WaitAll(this ValueTask task1, ValueTask task2, ValueTask task3)
        {
            var awaiter = new ValueTaskWaitAll<Triple<ValueTask>>(new Triple<ConfiguredValueTaskAwaitable> { 
                First = task1.ConfigureAwait(false), 
                Second = task2.ConfigureAwait(false),
                Third = task3.ConfigureAwait(false)
            }, Triple<ConfiguredTaskAwaitable>.Count);
            return awaiter.Start();
        }

        public static ValueTask WaitAll(this ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4)
        {
            var awaiter = new ValueTaskWaitAll<Quadruple<ValueTask>>(new Quadruple<ConfiguredValueTaskAwaitable> { 
                First = task1.ConfigureAwait(false), 
                Second = task2.ConfigureAwait(false),
                Third = task3.ConfigureAwait(false),
                Fourth = task4.ConfigureAwait(false)
            }, Quadruple<ConfiguredTaskAwaitable>.Count);
            return awaiter.Start();
        }

        public static ValueTask WaitAll(this ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5)
        {
            var awaiter = new ValueTaskWaitAll<Quintuple<ValueTask>>(new Quintuple<ConfiguredValueTaskAwaitable> { 
                First = task1.ConfigureAwait(false), 
                Second = task2.ConfigureAwait(false),
                Third = task3.ConfigureAwait(false),
                Fourth = task4.ConfigureAwait(false),
                Fifth = task5.ConfigureAwait(false)
            }, Quintuple<ConfiguredTaskAwaitable>.Count);
            return awaiter.Start();
        }

        public static ValueTask WaitAll(this ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6)
        {
            var awaiter = new ValueTaskWaitAll<Sextuple<ValueTask>>(new Sextuple<ConfiguredValueTaskAwaitable> { 
                First = task1.ConfigureAwait(false), 
                Second = task2.ConfigureAwait(false),
                Third = task3.ConfigureAwait(false),
                Fourth = task4.ConfigureAwait(false),
                Fifth = task5.ConfigureAwait(false),
                Sixth = task6.ConfigureAwait(false)
            }, Sextuple<ConfiguredTaskAwaitable>.Count);
            return awaiter.Start();
        }

        public static ValueTask WaitAll(this ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7)
        {
            var awaiter = new ValueTaskWaitAll<Septuple<ValueTask>>(new Septuple<ConfiguredValueTaskAwaitable> { 
                First = task1.ConfigureAwait(false), 
                Second = task2.ConfigureAwait(false),
                Third = task3.ConfigureAwait(false),
                Fourth = task4.ConfigureAwait(false),
                Fifth = task5.ConfigureAwait(false),
                Sixth = task6.ConfigureAwait(false),
                Seventh = task7.ConfigureAwait(false)
            }, Septuple<ConfiguredTaskAwaitable>.Count);
            return awaiter.Start();
        }

        public static ValueTask WaitAll(this ValueTask task1, ValueTask task2, ValueTask task3, ValueTask task4, ValueTask task5, ValueTask task6, ValueTask task7, ValueTask task8)
        {
            var awaiter = new ValueTaskWaitAll<Octuple<ConfiguredValueTaskAwaiter>>(new Octuple<ConfiguredValueTaskAwaiter> { 
                First = task1.ConfigureAwait(false).GetAwaiter(), 
                Second = task2.ConfigureAwait(false),
                Third = task3.ConfigureAwait(false),
                Fourth = task4.ConfigureAwait(false),
                Fifth = task5.ConfigureAwait(false),
                Sixth = task6.ConfigureAwait(false),
                Seventh = task7.ConfigureAwait(false),
                Eighth = task8.ConfigureAwait(false)
            }, Octuple<ConfiguredTaskAwaitable>.Count);
            return awaiter.Start();
        }

        public static ValueTask WaitAll<T>(this Func<T> enumeratorFactory)
            where T : struct, IEnumerator<ValueTask>
        {
            
        }
    }
}