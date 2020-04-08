using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using static System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    internal interface IExchange
    {
        //false to complete exchange
        //true to call CreateOutboundMessageAsync
        ValueTask<bool> ProcessInboundMessageAsync(PacketHeaders headers, ReadOnlyMemory<byte> payload, EndPoint endPoint, CancellationToken token);

        //false to complete exchange
        //true to wait for incoming messages
        ValueTask<(PacketHeaders Headers, int BytesWritten, bool)> CreateOutboundMessageAsync(Memory<byte> buffer, CancellationToken token);
    
        void OnException(Exception e);

        void OnCanceled(CancellationToken token);

        internal static int WriteResult(in Result<bool> result, Span<byte> output)
        {
            WriteInt64LittleEndian(output, result.Term);
            output[sizeof(long)] = result.Value.ToByte();
            return sizeof(long) + 1;
        }

        internal static Result<bool> ReadResult(ReadOnlySpan<byte> input)
            => new Result<bool>(ReadInt64LittleEndian(input), ValueTypeExtensions.ToBoolean(input[sizeof(long)]));
    }
}