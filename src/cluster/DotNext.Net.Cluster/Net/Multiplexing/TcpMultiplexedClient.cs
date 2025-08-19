using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace DotNext.Net.Multiplexing;

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
[Experimental("DOTNEXT001")]
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
        var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeoutSource.CancelAfter(connectTimeout);
        try
        {
            await socket.ConnectAsync(address, timeoutSource.Token).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            socket.Dispose();

            if (e is OperationCanceledException canceledEx
                && canceledEx.CancellationToken == timeoutSource.Token
                && token.IsCancellationRequested)
            {
                throw new OperationCanceledException(token);
            }
            else
            {
                throw;
            }
        }
        finally
        {
            timeoutSource.Dispose();
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