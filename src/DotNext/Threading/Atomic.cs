using System;
using System.Runtime.CompilerServices;

namespace DotNext.Threading
{
    internal abstract class Atomic<T>
    {
        internal abstract T Exchange(ref T value, T update);

        internal abstract T VolatileRead(ref T value);

        private protected abstract bool Equals(T x, T y);

        internal abstract T CompareExchange(ref T value, T update, T expected);

        internal bool CompareAndSet(ref T value, T expected, T update) 
            => Equals(CompareExchange(ref value, update, expected), expected);
        

        internal (T OldValue, T NewValue) Update(ref T value, Func<T, T> updater)
        {
            T oldValue, newValue;
            do
            {
                newValue = updater(oldValue = VolatileRead(ref value));
            }
            while (!CompareAndSet(ref value, oldValue, newValue));
            return (oldValue, newValue);
        }

        internal (T OldValue, T NewValue) Accumulate(ref T value, T x, Func<T, T, T> accumulator)
        {
            T oldValue, newValue;
            do
            {
                newValue = accumulator(oldValue = VolatileRead(ref value), x);
            }
            while (!CompareAndSet(ref value, oldValue, newValue));
            return (oldValue, newValue);
        }
    }

    internal static class Atomic<T, V, W>
        where W : struct, IAtomicWrapper<T, V>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static V CompareExchange(ref W wrapper, ref T value, V update, V expected)
            => wrapper.Convert(wrapper.Atomic.CompareExchange(ref value, wrapper.Convert(update), wrapper.Convert(expected)));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool CompareAndSet(ref W wrapper, ref T value, V expected, V update)
            => wrapper.Atomic.CompareAndSet(ref value, wrapper.Convert(expected), wrapper.Convert(update));
        
        internal static (V OldValue, V NewValue) Update(ref W wrapper, ref T value, Func<V, V> updater)
        {
            V oldValue, newValue;
            do
            {
                newValue = updater(oldValue = wrapper.Convert(wrapper.Atomic.VolatileRead(ref value)));
            }
            while (!CompareAndSet(ref wrapper, ref value, oldValue, newValue));
            return (oldValue, newValue);
        }

        internal static (V OldValue, V NewValue) Accumulate(ref W wrapper, ref T value, V x, Func<V, V, V> accumulator)
        {
            V oldValue, newValue;
            do
            {
                newValue = accumulator(oldValue = wrapper.Convert(wrapper.Atomic.VolatileRead(ref value)), x);
            }
            while (!CompareAndSet(ref wrapper, ref value, oldValue, newValue));
            return (oldValue, newValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static V GetAndSet(ref W wrapper, ref T value, V update) 
            => wrapper.Convert(wrapper.Atomic.Exchange(ref value, wrapper.Convert(update)));
    }
}
