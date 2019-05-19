using System;

namespace DotNext.Threading
{
    internal abstract class Atomic<T>
    {
        internal abstract bool CompareAndSet(ref T value, T expected, T update);
        private protected abstract T VolatileRead(ref T value);

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
}
