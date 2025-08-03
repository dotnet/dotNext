using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;

namespace DotNext.Net.Multiplexing;

using Threading;

/// <summary>
/// Represents a client-side of the multiplexing protocol on top of TCP.
/// </summary>
/// <param name="address">The address of the server.</param>
/// <param name="configuration">The configuration of the client.</param>
public class TcpMultiplexedClient(EndPoint address, TcpMultiplexedClient.Options configuration) : MultiplexedClient(configuration)
{
    private readonly TimeSpan connectTimeout = configuration.ConnectTimeout;

    /// <inheritdoc/>
    protected sealed override async ValueTask<Socket> ConnectAsync(CancellationToken token)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeoutSource.CancelAfter(connectTimeout);
        try
        {
            await socket.ConnectAsync(address, timeoutSource.Token).ConfigureAwait(false);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
        finally
        {
            timeoutSource.Dispose();
        }

        return socket;
    }

    /// <summary>
    /// Represents configuration of TCP multiplexing protocol client. 
    /// </summary>
    public new class Options : MultiplexedClient.Options
    {
        private readonly TimeSpan connectTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets connection timeout.
        /// </summary>
        public TimeSpan ConnectTimeout
        {
            get => connectTimeout;
            init
            {
                Threading.Timeout.Validate(value);

                connectTimeout = value;
            }
        }
    }
}