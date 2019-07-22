using System;
using System.Runtime.CompilerServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;

namespace DotNext.Threading
{
    internal abstract class Atomic<T>//T should not be greater than maximum size of primitive type. For .NET Standard it is sizeof(long)
    {
        internal abstract T Exchange(ref T value, T update);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static T Read(ref T value)
        {
            Push(ref value);
            Volatile();
            Ldobj(typeof(T));
            return Return<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Write(ref T storage, T value)
        {
            Push(ref storage);
            Push(value);
            Volatile();
            Stobj(typeof(T));
            Ret();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool Equals(T x, T y)
        {
            Push(x);
            Push(y);
            Ceq();
            return Return<bool>();
        }

        internal abstract T CompareExchange(ref T value, T update, T expected);

        internal bool CompareAndSet(ref T value, T expected, T update)
            => Equals(CompareExchange(ref value, update, expected), expected);

        internal (T OldValue, T NewValue) Update(ref T value, FunctionPointer<T, T> updater)
        {
            T oldValue, newValue;
            do
            {
                newValue = updater.Invoke(oldValue = Read(ref value));
            }
            while (!CompareAndSet(ref value, oldValue, newValue));
            return (oldValue, newValue);
        }

        internal (T OldValue, T NewValue) Accumulate(ref T value, T x, FunctionPointer<T, T, T> accumulator)
        {
            T oldValue, newValue;
            do
            {
                newValue = accumulator.Invoke(oldValue = Read(ref value), x);
            }
            while (!CompareAndSet(ref value, oldValue, newValue));
            return (oldValue, newValue);
        }
    }

    internal static class Atomic<T, V, W>
        where T : struct
        where V : struct
        where W : struct, IAtomicWrapper<T, V>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static V CompareExchange(ref W wrapper, V update, V expected)
            => wrapper.Convert(wrapper.Atomic.CompareExchange(ref wrapper.Reference, wrapper.Convert(update), wrapper.Convert(expected)));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool CompareAndSet(ref W wrapper, V expected, V update)
            => wrapper.Atomic.CompareAndSet(ref wrapper.Reference, wrapper.Convert(expected), wrapper.Convert(update));

        internal static (V OldValue, V NewValue) Update(ref W wrapper, FunctionPointer<V, V> updater)
        {
            V oldValue, newValue;
            do
            {
                newValue = updater.Invoke(oldValue = wrapper.Convert(Atomic<T>.Read(ref wrapper.Reference)));
            }
            while (!CompareAndSet(ref wrapper, oldValue, newValue));
            return (oldValue, newValue);
        }

        internal static (V OldValue, V NewValue) Accumulate(ref W wrapper, V x, FunctionPointer<V, V, V> accumulator)
        {
            V oldValue, newValue;
            do
            {
                newValue = accumulator.Invoke(oldValue = wrapper.Convert(Atomic<T>.Read(ref wrapper.Reference)), x);
            }
            while (!CompareAndSet(ref wrapper, oldValue, newValue));
            return (oldValue, newValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static V GetAndSet(ref W wrapper, ref T value, V update)
            => wrapper.Convert(wrapper.Atomic.Exchange(ref value, wrapper.Convert(update)));
    }
}
