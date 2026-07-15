namespace DotNext.Threading;

partial class Atomic
{
    /// <summary>
    /// Extends <see cref="Atomic{T}"/> type that holds optional value.
    /// </summary>
    /// <param name="atomic">The receiver.</param>
    /// <typeparam name="T">The type of the optional value.</typeparam>
    extension<T>(Atomic<Optional<T>> atomic)
    {
        /// <summary>
        /// Sets the value to if it's not defined in the receiver.
        /// </summary>
        /// <param name="value">The value to set.</param>
        /// <returns><see langword="true"/> if <paramref name="value"/> is set to the empty receiver; otherwise, <see langword="false"/>.</returns>
        public bool TrySet(T value)
        {
            var newValue = new Optional<T>(value);
            return atomic.TrySet(in newValue);
        }

        private bool TrySet(in Optional<T> newValue)
        {
            for (var spinner = new SpinWait();; spinner.SpinOnce())
            {
                var stamp = atomic.Read(ref spinner, out var currentValue);
                if (!currentValue.IsUndefined)
                    return false;

                if (atomic.TryWrite(stamp, in newValue))
                    return true;
            }
        }

        /// <summary>
        /// Returns the value if it is <see cref="Optional{T}.IsUndefined">defined</see>;
        /// or sets the supplied value.
        /// </summary>
        /// <param name="value">The value to set if the receiver is undefined.</param>
        /// <param name="isSet">
        /// <see langword="true"/> if <paramref name="value"/> is written to the container;
        /// <see langword="false"/> if the receiver already has a value and <paramref name="value"/> is ignored.
        /// </param>
        /// <returns>The existing value contained in the receiver; or <paramref name="value"/>.</returns>
        public T GetOrSet(T value, out bool isSet)
        {
            var newValue = new Optional<T>(value);
            return atomic.GetOrSet(in newValue, out isSet);
        }

        private T GetOrSet(in Optional<T> newValue, out bool isSet)
        {
            for (var spinner = new SpinWait();; spinner.SpinOnce())
            {
                var stamp = atomic.Read(ref spinner, out var currentValue);
                if (!currentValue.IsUndefined)
                {
                    isSet = false;
                    return currentValue.ValueOrDefault!;
                }

                if (atomic.TryWrite(stamp, in newValue))
                {
                    isSet = true;
                    return newValue.ValueOrDefault!;
                }
            }
        }
    }
}