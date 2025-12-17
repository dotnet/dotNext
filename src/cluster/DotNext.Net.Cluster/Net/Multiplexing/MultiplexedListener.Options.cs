using static System.Threading.Timeout;

namespace DotNext.Net.Multiplexing;

partial class MultiplexedListener
{
    /// <summary>
    /// Represents multiplexed listener configuration.
    /// </summary>
    public class Options : MultiplexingOptions
    {
        /// <summary>
        /// Gets or sets the send/receive timeout.
        /// </summary>
        public TimeSpan Timeout
        {
            get;
            init
            {
                Threading.Timeout.Validate(value);

                field = value;
            }
        } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Determines a drift between heartbeat packet and regular network timeout.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not in range (0..1).</exception>
        public double HeartbeatDrift
        {
            get;
            init => field = double.IsNormal(value) && value is > 0D and < 1D
                ? value
                : throw new ArgumentOutOfRangeException(nameof(value));
        } = .5D;

        internal TimeSpan HeartbeatTimeout => Timeout == InfiniteTimeSpan
            ? InfiniteTimeSpan
            : Timeout * HeartbeatDrift;
    }
}