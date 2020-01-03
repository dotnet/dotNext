using System;

namespace DotNext.Threading
{
    /// <summary>
    /// Represents lock options.
    /// </summary>
    public class DistributedLockOptions
    {
        private TimeSpan? leaseTime;

        /// <summary>
        /// Gets or sets maximum lease duration for the acquired lock.
        /// </summary>
        /// <remarks>
        /// The lock holder must acquire its ownership periodically
        /// in this time window.
        /// </remarks>
        public TimeSpan LeaseTime
        {
            get => leaseTime ?? TimeSpan.FromMinutes(1);
            set => leaseTime = value;
        }
    }
}
