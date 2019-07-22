using System.Threading;

namespace DotNext.Threading
{
    /// <summary>
    /// Provides volatile access to non-primitive data type.
    /// </summary>
    public struct VolatileContainer<T>
        where T : struct
    {
        private int readCount;
        private T value;
        private readonly SpinWait spinner;

        /// <summary>
        /// Gets or sets value in volatile manner.
        /// </summary>
        public T Value
        {
            get
            {
                T result;
                try_again:
                var currentCount = readCount.VolatileRead();
                if(currentCount >= 0 && readCount.CompareAndSet(currentCount, currentCount + 1))
                {
                    result = value;
                    readCount.DecrementAndGet();
                }
                else
                {
                    spinner.SpinOnce();
                    goto try_again;
                }
                return result;
            }
            set
            {
                try_again:
                if(readCount.CompareAndSet(0, int.MaxValue))
                {
                    this.value = value;
                    readCount.VolatileWrite(0);
                }
                else
                {
                    spinner.SpinOnce();
                    goto try_again;
                }
            }
        }
    }
}