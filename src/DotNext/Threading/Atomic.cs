using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Threading.Interlocked;
using SpinWait = System.Threading.SpinWait;

namespace DotNext.Threading
{
    using static Runtime.InteropServices.Memory;

    /// <summary>
    /// Provides atomic access to non-primitive data type.
    /// </summary>
    /// <remarks>
    /// Synchronized methods can be declared in classes only. If you don't need to have extra heap allocation
    /// to keep synchronization root in the form of the object or you need to have volatile field
    /// inside of value type then <see cref="Atomic{T}"/> is the best choice. Its performance is better
    /// than synchronized methods according with benchmarks.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    [SuppressMessage("Performance", "CA1815", Justification = "This type is a container for atomic access and cannot be compared with other containers due to race conditions")]
    public struct Atomic<T> : IStrongBox, ICloneable
        where T : struct
    {
        /// <summary>
        /// Represents atomic update action.
        /// </summary>
        /// <remarks>The atomic update action should side-effect free.</remarks>
        /// <param name="current">The value to update.</param>
        public delegate void Updater(ref T current);

        /// <summary>
        /// Represents atomic accumulator.
        /// </summary>
        /// <remarks>The atomic accumulator should side-effect free.</remarks>
        /// <param name="current">The value to update.</param>
        /// <param name="x">The value to be combined with <paramref name="current"/>.</param>
        public delegate void Accumulator(ref T current, in T x);

        private T value;

        private AtomicBoolean lockState;
        private volatile int version;    //used for optimistic lock

        /// <summary>
        /// Clones this container atomically.
        /// </summary>
        /// <returns>The cloned container.</returns>
        public Atomic<T> Clone()
        {
            var result = new Atomic<T>();
            Read(out result.value);
            return result;
        }

        object ICloneable.Clone() => Clone();

        /// <summary>
        /// Performs atomic read.
        /// </summary>
        /// <param name="result">The result of atomic read.</param>
        public void Read(out T result)
        {
            SpinWait spinner;
            spin_loop:
            var stamp = version;
            Copy(in value, out result);
            if (stamp != version || lockState.Value)
            {
                spinner.SpinOnce();
                goto spin_loop;
            }
        }

        /// <summary>
        /// Swaps the value stored in this container and the given value atomically.
        /// </summary>
        /// <remarks>
        /// This operation is atomic for both containers.
        /// </remarks>
        /// <param name="other">The container for the value.</param>
        public void Swap(ref Atomic<T> other)
        {
            lockState.Acquire();
            Increment(ref version);
            Swap(ref other.value);
            lockState.Release();
        }

        /// <summary>
        /// Swaps the value stored in this container and the given value atomically.
        /// </summary>
        /// <param name="other">The managed pointer to the value to swap.</param>
        public void Swap(ref T other)
        {
            lockState.Acquire();
            Increment(ref version);
            Runtime.InteropServices.Memory.Swap(ref value, ref other);
            lockState.Release();
        }

        /// <summary>
        /// Performs atomic write.
        /// </summary>
        /// <param name="newValue">The value to be stored into this container.</param>
        public void Write(in T newValue)
        {
            lockState.Acquire();
            Increment(ref version);
            Copy(in newValue, out value);
            lockState.Release();
        }

        /// <summary>
        /// Compares two values of type <typeparamref name="T"/> for bitwise equality and, if they are equal, replaces the stored value.
        /// </summary>
        /// <param name="update">The value that replaces the stored value if the comparison results in equality.</param>
        /// <param name="expected">The value that is compared to the stored value.</param>
        /// <param name="result">The origin value stored in this container before modification.</param>
        public void CompareExchange(in T update, in T expected, out T result)
        {
            lockState.Acquire();
            Increment(ref version);
            var current = value;
            if (BitwiseComparer<T>.Equals(current, expected))
                Copy(in update, out value);
            Copy(in current, out result);
            lockState.Release();
        }

        /// <summary>
        /// Atomically sets the stored value to the given updated value if the current value == the expected value.
        /// </summary>
        /// <param name="expected">The expected value.</param>
        /// <param name="update">The new value.</param>
        /// <returns><see langword="true"/> if successful. <see langword="false"/> return indicates that the actual value was not equal to the expected value.</returns>
        public bool CompareAndSet(in T expected, in T update)
        {
            lockState.Acquire();
            Increment(ref version);
            bool result;
            if (result = BitwiseComparer<T>.Equals(value, expected))
                Copy(in update, out value);
            lockState.Release();
            return result;
        }

        /// <summary>
        /// Sets a value stored in this container to a specified value and returns the original value, as an atomic operation.
        /// </summary>
        /// <param name="update">The value that replaces the stored value.</param>
        /// <param name="previous">The original stored value before modification.</param>
        public void Exchange(in T update, out T previous)
        {
            lockState.Acquire();
            Increment(ref version);
            Copy(in value, out previous);
            Copy(in update, out value);
            lockState.Release();
        }

        /// <summary>
        /// Atomically updates the stored value with the results of applying the given function, returning the updated value.
        /// </summary>
        /// <param name="updater">A side-effect-free function</param>
        /// <param name="result">The updated value.</param>
        /// <exception cref="ArgumentNullException"><paramref name="updater"/> is <see langword="null"/>.</exception>
        public void UpdateAndGet(Updater updater, out T result)
        {
            if (updater is null)
                throw new ArgumentNullException(nameof(updater));
            lockState.Acquire();
            Increment(ref version);
            try
            {
                updater(ref value);
                Copy(in value, out result);
            }
            finally
            {
                lockState.Release();
            }
        }

        /// <summary>
        /// Atomically updates the stored value with the results 
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <param name="updater">A side-effect-free function</param>
        /// <param name="result">The original value.</param>
        /// <exception cref="ArgumentNullException"><paramref name="updater"/> is <see langword="null"/>.</exception>
        public void GetAndUpdate(Updater updater, out T result)
        {
            if (updater is null)
                throw new ArgumentNullException(nameof(updater));
            lockState.Acquire();
            Increment(ref version);
            var previous = value;
            try
            {
                updater(ref value);
                Copy(in previous, out result);
            }
            finally
            {
                lockState.Release();
            }
        }

        /// <summary>
        /// Atomically updates the stored value with the results of applying the given function 
        /// to the current and given values, returning the original value.
        /// </summary>
        /// <remarks>
        /// The function is applied with the current value as its first argument, and the given update as the second argument.
        /// </remarks>
        /// <param name="x">Accumulator operand.</param>
        /// <param name="accumulator">A side-effect-free function of two arguments</param>
        /// <param name="result">The updated value.</param>
        /// <exception cref="ArgumentNullException"><paramref name="accumulator"/> is <see langword="null"/>.</exception>
        public void AccumulateAndGet(in T x, Accumulator accumulator, out T result)
        {
            if (accumulator is null)
                throw new ArgumentNullException(nameof(accumulator));
            lockState.Acquire();
            Increment(ref version);
            try
            {
                accumulator(ref value, in x);
                Copy(in value, out result);
            }
            finally
            {
                lockState.Release();
            }
        }

        /// <summary>
        /// Atomically updates the stored value with the results of applying the given function 
        /// to the current and given values, returning the updated value.
        /// </summary>
        /// <remarks>
        /// The function is applied with the current value as its first argument, and the given update as the second argument.
        /// </remarks>
        /// <param name="x">Accumulator operand.</param>
        /// <param name="accumulator">A side-effect-free function of two arguments</param>
        /// <param name="result">The original value.</param>
        /// <exception cref="ArgumentNullException"><paramref name="accumulator"/> is <see langword="null"/>.</exception>
        public void GetAndAccumulate(in T x, Accumulator accumulator, out T result)
        {
            if (accumulator is null)
                throw new ArgumentNullException(nameof(accumulator));
            lockState.Acquire();
            Increment(ref version);
            var previous = value;
            try
            {
                accumulator(ref value, in x);
                Copy(in previous, out result);
            }
            finally
            {
                lockState.Release();
            }
        }

        /// <summary>
        /// Gets or sets value atomically.
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

        /// <summary>
        /// Converts the stored value into string atomically.
        /// </summary>
        /// <returns>The string returned from <see cref="object.ToString"/> method called on the stored value.</returns>
        public override string ToString()
        {
            Read(out var result);
            return result.ToString();
        }
    }
}
