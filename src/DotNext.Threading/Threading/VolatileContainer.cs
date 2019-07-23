using System.Runtime.CompilerServices;
using System.Threading;

namespace DotNext.Threading
{
    using static Runtime.InteropServices.Memory;

    /// <summary>
    /// Provides volatile access to non-primitive data type.
    /// </summary>
    /// <remarks>
    /// Synchronized methods can be declared in classes only. If you don't need to have extra heap allocation
    /// to keep synchronization root in the form of the object or you need to have volatile field
    /// inside of value type then <c>VolatileContainer</c> is the best choice. Its performance is better
    /// than synchronized methods according with benchmarks.
    /// </remarks>
    public struct VolatileContainer<T> : IStrongBox
        where T : struct
    {
        /// <summary>
        /// Represents equality comparer.
        /// </summary>
        /// <param name="first">The first value to compare.</param>
        /// <param name="second">The second value to compare.</param>
        /// <returns><see langword="true"/> if <paramref name="first"/> is equal to <paramref name="second"/>; otherwise, <see langword="false"/>.</returns>
        public delegate bool EqualityComparer(in T first, in T second);

        private T value;

        private AtomicBoolean state;

        /// <summary>
        /// Performs atomic read.
        /// </summary>
        /// <param name="result">The result of volatile read.</param>
        public void Read(out T result)
        {
            for(SpinWait spinner; !state.FalseToTrue(); spinner.SpinOnce()) {}
            Copy(in value, out result);
            state.Value = false;
        }

        /// <summary>
        /// Performs atomic write.
        /// </summary>
        /// <param name="value">The value to be stored into this container.</param>
        public void Write(in T value)
        {
            for(SpinWait spinner; !state.FalseToTrue(); spinner.SpinOnce()) {}
            Copy(in value, out this.value);
            state.Value = false;
        }

        public void CompareExchange(in T update, in T expected, EqualityComparer comparer, out T result)
        {
            for(SpinWait spinner; !state.FalseToTrue(); spinner.SpinOnce()) {}
            var current = value;
            if(comparer(in current, in expected))
                Copy(in update, out value);
            Copy(in current, out result);
            state.Value = false;
        }

        public bool CompareAndSet(in T expected, in T update, EqualityComparer comparer)
        {
            for(SpinWait spinner; !state.FalseToTrue(); spinner.SpinOnce()) {}
            if(comparer(in value, in expected))
                Copy(in update, out value);
            state.Value = false;
        }

        public void Exchange(in T update, out T previous)
        {
            for(SpinWait spinner; !state.FalseToTrue(); spinner.SpinOnce()) {}
            Copy(in value, out previous);
            Copy(in update, out value);
            state.Value = false;
        }

        private void Update(in T update, FunctionPointer<T, T> updater, EqualityComparer comparer, out T result, bool newValueExpected)
        {
            T newValue, oldValue;
            do
            {
                Read(out oldValue);
                newValue = updater.Invoke(oldValue);
            }
            while (!CompareAndSet(in oldValue, in newValue, comparer));
            result = newValueExpected ? newValue : oldValue;
        }

        public void UpdateAndGet(in T update, FunctionPointer<T, T> updater, EqualityComparer comparer, out T result)
            => Update(in update, updater, comparer, out result, true);

        public void GetAndUpdate(in T update, FunctionPointer<T, T> updater, EqualityComparer comparer, out T result)
            => Update(in update, updater, comparer, out result, false);

        private void Accumulate(ref T value, T x, FunctionPointer<T, T, T> accumulator, EqualityComparer comparer, out T result, bool newValueExpected)
        {
            T oldValue, newValue;
            do
            {
                Read(out oldValue);
                newValue = accumulator.Invoke(oldValue, x);
            }
            while (!CompareAndSet(oldValue, newValue, comparer));
            result = newValueExpected ? newValue : oldValue;
        }

        /// <summary>
        /// Gets or sets value in volatile manner.
        /// </summary>
        /// <remarks>
        /// To achieve best performance it is recommended to use <see cref="Read"/> and <see cref="Write"/> methods
        /// because they don't cause extra allocation of stack memory for passing value.
        /// </remarks>
        public T Value
        {
            get
            {
                Read(out var result);
                return result;
            }
            set => Write(value);
        }

        object IStrongBox.Value
        {
            get => Value;
            set => Value = (T)value;
        }
    }
}