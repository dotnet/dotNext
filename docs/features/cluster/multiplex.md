Multiplexing
====

.NEXT exposes a simple multiplexing protocol on top of TCP, so the single TCP connection can be shared between the independent components of the application. Its design is very similar to the other implementations:

* [yamux](https://github.com/hashicorp/yamux)
* [smux](https://github.com/xtaci/smux)

API surface is represented by the following classes:
* [TcpMultiplexedListener](xref:DotNext.Net.Multiplexing.TcpMultiplexedListener) represents server-side multiplexer
* [TcpMultiplexedClient](xref:DotNext.Net.Multiplexing.TcpMultiplexedClient) represents client-side multiplexer

Every logical channel (stream) within the connection is represented as [IDuplexPipe](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipelines.iduplexpipe). To complete the pipe, the caller needs to complete both [Input](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipelines.iduplexpipe.input) and [Output](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipelines.iduplexpipe.output) parts of the pipe.

The implementation has the following features and limitations:
* Backpressure is preserved if the sender is faster than the receiver
* Every channel is scheduled in fair manner. Data from the multiple channels are grouped as frames with the fixed maximum size. Multiple frames are combined into multiplexed buffer before sending. This approach reduces a number of necessary system calls. Nagle algorithm is disabled.
* Frame has very low overhead, ~ 8 bytes
* Sender can inform the other end that no more data expected
* No encryption (use WireGuard, VPN, IPSec)
* All channels are marked as completed (with exception) if the underlying TCP connection is timed out or closed
* The maximum number of pending channels waiting to be accepted can be limited on the server-side
* The channel is a duplex pipe

Keep the channel alive as long as possible. Don't use the channel just for a single pair of request/response. In the case of TCP connection failure, the implementation tries to reconnect automatically in the background. The channel just reports the error. In that case, the caller needs to request a new channel.

# Client
[TcpMultiplexedClient](xref:DotNext.Net.Multiplexing.TcpMultiplexedClient) represents client-side API of the multiplexing protocol:
* `StartAsync` starts the connection to the server asynchronously. The client detects connection failures automatically and triggers reconnection in the background
* Once the client has started, the stream can be obtained with `OpenStreamAsync` method, which returns [IDuplexPipe](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipelines.iduplexpipe)

When the pipe is no longer needed, then it should be closed by calling `Complete` or `CompleteAsync` methods on its input and output. Otherwise, the protocol considers the channel as opened.

# Server
[TcpMultiplexedListener](xref:DotNext.Net.Multiplexing.TcpMultiplexedListener) represents server-side API of the multiplexing protocol:
* `StartAsync` starts listening for incoming connections
* Once the server has started, the incoming stream can be accepted with `AcceptAsync` method, which returns [IDuplexPipe](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipelines.iduplexpipe)

When the pipe is no longer needed, then it should be closed by calling `Complete` or `CompleteAsync` methods on its input and output. Otherwise, the protocol considers the channel as opened.