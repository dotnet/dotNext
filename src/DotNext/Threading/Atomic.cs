using System;

namespace DotNext.Threading
{
	using Generic;

	internal static class Atomic<T, CAS>
		where CAS : Constant<CAS<T>>, new()
	{
		private static readonly CAS<T> CompareAndSet = new CAS();

		internal static (T OldValue, T NewValue) Update(ref T value, Func<T, T> updater)
		{
			T oldValue, newValue;
			do
			{
				newValue = updater(oldValue = value);
			} 
			while (!CompareAndSet(ref value, oldValue, newValue));
			return (oldValue, newValue);
		}

		internal static (T OldValue, T NewValue) Accumulute(ref T value, T x, Func<T, T, T> accumulator)
		{
			T oldValue, newValue;
			do
			{
				newValue = accumulator(oldValue = value, x);
			} 
			while (!CompareAndSet(ref value, oldValue, newValue));
			return (oldValue, newValue);
		}
	}
}
