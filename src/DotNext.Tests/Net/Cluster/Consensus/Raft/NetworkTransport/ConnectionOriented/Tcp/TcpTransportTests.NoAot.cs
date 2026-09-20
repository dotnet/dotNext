using Microsoft.Extensions.Logging;

namespace DotNext.Net.Cluster.Consensus.Raft.NetworkTransport.ConnectionOriented.Tcp;

using DotNext.Extensions.Logging;

partial class TcpTransportTests
{
    private static ILoggerFactory CreateDebugLoggerFactory(int port)
        => TestLoggers.CreateDebugLoggerFactory(port.ToString(), static builder => builder.SetMinimumLevel(LogLevel.Debug));
}