using static System.Threading.Timeout;

namespace DotNext.Net.Multiplexing;

using Threading;

partial class MultiplexedListener
{
    public class Options : MultiplexingOptions
    {
        private readonly TimeSpan receiveTimeout = TimeSpan.FromSeconds(60);
        private readonly double heartbeatDrift = .5D;
        private readonly int backlog = 100;
        
        /// <summary>
        /// Gets or sets the send/receive timeout.
        /// </summary>
        public TimeSpan Timeout
        {
            get => receiveTimeout;
            init
            {
                Threading.Timeout.Validate(value);

                receiveTimeout = value;
            }
        }
        
        /// <summary>
        /// Determines a drift between heartbeat packet and regular network timeout.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not in range (0..1).</exception>
        public double HeartbeatDrift
        {
            get => heartbeatDrift;
            init => heartbeatDrift = double.IsNormal(value) && value is > 0D and < 1D
                ? value
                : throw new ArgumentOutOfRangeException(nameof(value));
        }

        internal TimeSpan HeartbeatTimeout => receiveTimeout == InfiniteTimeSpan
            ? InfiniteTimeSpan
            : receiveTimeout * HeartbeatDrift;

        /// <summary>
        /// Gets or sets the maximum amount of pending streams in the backlog.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than or equal to zero.</exception>
        public int Backlog
        {
            get => backlog;
            init => backlog = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}