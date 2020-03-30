using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    internal interface IExchange
    {
        //false to complete exchange
        //true to call CreateOutboundMessageAsync
        ValueTask<bool> ProcessInbountMessageAsync(PacketHeaders headers, ReadOnlyMemory<byte> payload, EndPoint endPoint, CancellationToken token);

        //false to complete exchange
        //true to wait for incoming messages
        ValueTask<(PacketHeaders Headers, int BytesWritten, bool)> CreateOutboundMessageAsync(Memory<byte> buffer, CancellationToken token);
    
        void OnException(Exception e);

        void OnCanceled(CancellationToken token);
    }
}