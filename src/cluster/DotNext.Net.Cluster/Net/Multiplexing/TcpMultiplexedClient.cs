using System.Net;
using System.Net.Sockets;

namespace DotNext.Net.Multiplexing;

using Threading;

/// <summary>
/// Represents a client-side of the multiplexing protocol on top of TCP.
/// </summary>
/// <remarks>
/// TCP multiplexer provides unencrypted multiplexed transport on top of TCP/IP stack. It should
/// not be used for communication over the Internet or any other untrusted network. It's aimed for
/// efficient communication between cluster nodes within the trusted LAN.
/// </remarks>
/// <param name="address">The address of the server.</param>
/// <param name="configuration">The configuration of the client.</param>
public class TcpMultiplexedClient(EndPoint address, TcpMultiplexedClient.Options configuration) : MultiplexedClient(configuration), IPeer
{
    private readonly TimeSpan connectTimeout = configuration.ConnectTimeout;

    /// <inheritdoc/>
    protected sealed override async ValueTask<Socket> ConnectAsync(CancellationToken token)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true,
        };
        var timeoutSource = CombineTokens(connectTimeout, token, LifetimeToken);
        try
        {
            await socket.ConnectAsync(address, timeoutSource.Token).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            socket.Dispose();

            if (e is OperationCanceledException canceledEx)
            {
                if (canceledEx.CausedBy(timeoutSource, token))
                    throw new OperationCanceledException(token);

                if (canceledEx.CausedByTimeout(timeoutSource))
                    throw new TimeoutException();

                ObjectDisposedException.ThrowIf(canceledEx.CausedBy(timeoutSource, LifetimeToken), this);
            }

            throw;
        }
        finally
        {
            await timeoutSource.DisposeAsync().ConfigureAwait(false);
        }

        return socket;
    }

    /// <inheritdoc/>
    EndPoint IPeer.EndPoint => address;

    /// <summary>
    /// Represents configuration of TCP multiplexing protocol client. 
    /// </summary>
    public new class Options : MultiplexedClient.Options
    {
        /// <summary>
        /// Gets or sets connection timeout.
        /// </summary>
        public TimeSpan ConnectTimeout
        {
            get;
            init
            {
                Threading.Timeout.Validate(value);

                field = value;
            }
        } = TimeSpan.FromSeconds(30);
    }
}