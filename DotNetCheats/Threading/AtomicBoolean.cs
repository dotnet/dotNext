using System;
using System.Threading;
using System.Runtime.CompilerServices;

namespace Cheats.Threading
{
	/// <summary>
	/// Represents atomic boolean.
	/// </summary>
    [Serializable]
    public struct AtomicBoolean: IEquatable<bool>
    {
        private const int True = 1;
        private const int False = 0;
        private int value;

        public AtomicBoolean(bool value) => this.value = value ? True : False;

		/// <summary>
		/// Gets or sets boolean value in volatile manner.
		/// </summary>
        public bool Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => value.VolatileGet() == True;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => this.value.VolatileSet(value ? True: False);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CompareExchange(bool update, bool expected)
            => Interlocked.CompareExchange(ref value, update ? True : False, expected ? True : False) == True;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CompareAndSet(bool expected, bool update)
            => value.CompareAndSet(expected ? True : False, update ? True : False);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool FalseToTrue() => Interlocked.CompareExchange(ref value, True, False) == True;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrueToFalse() => Interlocked.CompareExchange(ref value, False, True) == False;

        private (int OldValue, int NewValue) Negate()
        {
            int oldValue, newValue;
            do
            {
                oldValue = value.VolatileGet();
                newValue = oldValue ^ True;
            } 
            while(Interlocked.CompareExchange(ref value, newValue, oldValue) != newValue);
            return (oldValue, newValue);
        }

        public bool NegateAndGet() => Negate().NewValue == True;

        public bool GetAndNegate() => Negate().OldValue == True;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetAndSet(bool value)
            => Interlocked.Exchange(ref this.value, value ? True: False) == True;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetAndGet(bool value)
        {
            Value = value;
            return value;
        }

        private (bool OldValue, bool NewValue) Accumulate(bool x, Func<bool, bool, bool> accumulator)
        {
            bool oldValue, newValue;
            do
            {
                newValue = accumulator(oldValue = Value, x);
            }
            while(!CompareAndSet(oldValue, newValue));
            return (oldValue, newValue);
        }

        private (bool OldValue, bool NewValue) Update(Func<bool, bool> updater)
        {
            bool oldValue, newValue;
            do
            {
                newValue = updater(oldValue = Value);
            }
            while(!CompareAndSet(oldValue, newValue));
            return (oldValue, newValue);
        }

        public bool AccumulateAndGet(bool x, Func<bool, bool, bool> accumulator)
            => Accumulate(x, accumulator).NewValue;

        public bool GetAndAcummulate(bool x, Func<bool, bool, bool> accumulator)
            => Accumulate(x, accumulator).OldValue;
        
        public bool UpdateAndGet(Func<bool, bool> updater)
            => Update(updater).NewValue;
        
        public bool GetAndUpdate(Func<bool, bool> updater)
            => Update(updater).OldValue;
        
        public bool Equals(bool other) => value == (other ? True : False);

        public override int GetHashCode() => value;

        public override bool Equals(object other)
        {
            switch(other)
            {
                case bool b:
                    return Equals(b);
                case AtomicBoolean b:
                    return b.value.VolatileGet() == value.VolatileGet();
                default:
                    return false;
            }
        }

        public override string ToString() => value == True ? Boolean.TrueString : Boolean.FalseString;
    }
}