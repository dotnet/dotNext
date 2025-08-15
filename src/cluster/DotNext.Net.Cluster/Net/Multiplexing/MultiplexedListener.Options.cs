using static System.Threading.Timeout;

namespace DotNext.Net.Multiplexing;

partial class MultiplexedListener
{
    /// <summary>
    /// Represents multiplexed listener configuration.
    /// </summary>
    public class Options : MultiplexingOptions
    {
        private readonly TimeSpan receiveTimeout = TimeSpan.FromSeconds(60);
        private readonly double heartbeatDrift = .5D;
        
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
    }
}