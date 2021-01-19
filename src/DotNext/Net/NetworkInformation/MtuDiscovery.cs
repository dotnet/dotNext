using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.NetworkInformation
{
    /// <summary>
    /// Allows to discover maximum size of Message Transfer Unit over IP network.
    /// </summary>
    /// <remarks>
    /// The discovered MTU size doesn't include size of IPv4 or IPv6 headers.
    /// </remarks>
    public class MtuDiscovery : Ping
    {
        private const int IcmpEchoHeaderSize = 8;

        /// <summary>
        /// Discovers maximum allowed MTU size by the underlying network.
        /// </summary>
        /// <param name="address">The destination host to which the MTU size should be discovered.</param>
        /// <param name="timeout">The maximum number of milliseconds to wait for the ICMP echo reply message. It's not a time-out of entire operation.</param>
        /// <param name="options">Discovery options.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The discovered MTU size, in bytes; or <see langword="null"/> if remote host is not reacheable.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is less than 0.</exception>
        /// <exception cref="InvalidOperationException">Discovery is in progress.</exception>
        /// <exception cref="PingException">An exception was thrown while sending or receiving the ICMP messages.</exception>
        /// <exception cref="System.Net.Sockets.SocketException"><paramref name="address"/> is not a valid address.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public int? Discover(IPAddress address, int timeout, MtuDiscoveryOptions options, CancellationToken token = default)
        {
            int mtuLowerBound = options.MinMtuSize, mtuUpperBound = options.MaxMtuSize;
            int? bestMtu = default;
            for (int currentMtu; mtuLowerBound <= mtuUpperBound; token.ThrowIfCancellationRequested())
            {
                currentMtu = (mtuLowerBound + mtuUpperBound) / 2;

                // TODO: Should be optimized. See https://github.com/dotnet/runtime/issues/34856
                var buffer = new byte[currentMtu];
                var reply = Send(address, timeout, buffer, options);
                switch (reply.Status)
                {
                    default:
                        return null;
                    case IPStatus.Success:
                        bestMtu = currentMtu + IcmpEchoHeaderSize;
                        mtuLowerBound = currentMtu + 1;
                        continue;
                    case IPStatus.TimedOut when Environment.OSVersion.Platform == PlatformID.Unix: // TODO: This is required due to https://github.com/dotnet/runtime/issues/34855
                    case IPStatus.PacketTooBig:
                        mtuUpperBound = currentMtu - 1;
                        continue;
                }
            }

            return bestMtu;
        }

        /// <summary>
        /// Discovers maximum allowed MTU size by the underlying network.
        /// </summary>
        /// <param name="address">The destination host to which the MTU size should be discovered.</param>
        /// <param name="timeout">The maximum number of milliseconds to wait for the ICMP echo reply message. It's not a time-out of entire operation.</param>
        /// <param name="options">Discovery options.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The discovered MTU size, in bytes; or <see langword="null"/> if remote host is not reacheable.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is less than 0.</exception>
        /// <exception cref="InvalidOperationException">Discovery is in progress.</exception>
        /// <exception cref="PingException">An exception was thrown while sending or receiving the ICMP messages.</exception>
        /// <exception cref="System.Net.Sockets.SocketException"><paramref name="address"/> is not a valid address.</exception>
        /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public async Task<int?> DiscoverAsync(IPAddress address, int timeout, MtuDiscoveryOptions options, CancellationToken token = default)
        {
            int mtuLowerBound = options.MinMtuSize, mtuUpperBound = options.MaxMtuSize;
            int? bestMtu = default;
            for (int currentMtu; mtuLowerBound <= mtuUpperBound; token.ThrowIfCancellationRequested())
            {
                currentMtu = (mtuLowerBound + mtuUpperBound) / 2;

                // TODO: Should be optimized. See https://github.com/dotnet/runtime/issues/34856
                var buffer = new byte[currentMtu];
                var reply = await SendPingAsync(address, timeout, buffer, options).ConfigureAwait(false);
                switch (reply.Status)
                {
                    default:
                        return null;
                    case IPStatus.Success:
                        bestMtu = currentMtu + IcmpEchoHeaderSize;
                        mtuLowerBound = currentMtu + 1;
                        continue;
                    case IPStatus.TimedOut when Environment.OSVersion.Platform == PlatformID.Unix: // TODO: This is required due to https://github.com/dotnet/runtime/issues/34855
                    case IPStatus.PacketTooBig:
                        mtuUpperBound = currentMtu - 1;
                        continue;
                }
            }

            return bestMtu;
        }
    }
}