using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    using Text;
    using StringLengthEncoding = IO.StringLengthEncoding;
    using static IO.Pipelines.PipeExtensions;
    using static IO.Pipelines.ResultExtensions;

    internal sealed class MetadataExchange : ClientExchange
    {
        private const StringLengthEncoding LengthEncoding = StringLengthEncoding.Compressed;

        private enum State : byte
        {
            Initial = 0,
            InputExpected,
            Final
        }

        private State state;

        internal MetadataExchange(long term)
            : base(term)
        {
        }

        private static Encoding Encoding => Encoding.UTF8;

        internal static async Task WriteAsync(PipeWriter writer, IReadOnlyDictionary<string, string> input, CancellationToken token)
        {
            //write length
            var lengthBytes = new byte[sizeof(int)];
            WriteInt32LittleEndian(lengthBytes, input.Count);
            var flushResult = await writer.WriteAsync(lengthBytes, token).ConfigureAwait(false);
            if(flushResult.IsCompleted)
                return;
            flushResult.ThrowIfCancellationRequested(token);
            //write pairs
            var context = new EncodingContext(Encoding, true);
            foreach(var (key, value) in input)
            {
                await writer.WriteStringAsync(key.AsMemory(), context, lengthFormat : LengthEncoding, token: token).ConfigureAwait(false);
                await writer.WriteStringAsync(value.AsMemory(), context, lengthFormat : LengthEncoding, token: token).ConfigureAwait(false);
            }
            await writer.CompleteAsync();
        }

        internal async Task ReadAsync(IDictionary<string, string> output, CancellationToken token)
        {
            //read length
            var lengthBytes = new byte[sizeof(int)];
            await Reader.ReadAsync(lengthBytes, token).ConfigureAwait(false);
            var length = ReadInt32LittleEndian(lengthBytes);
            var context = new DecodingContext(Encoding, true);
            while(--length >= 0)
            {
                //read key
                var key = await Reader.ReadStringAsync(LengthEncoding, context, token).ConfigureAwait(false);
                //read value
                var value = await Reader.ReadStringAsync(LengthEncoding, context, token).ConfigureAwait(false);
                //write pair to the dictionary
                output.Add(key, value);
            }
        }

        public override async ValueTask<bool> ProcessInbountMessageAsync(PacketHeaders headers, ReadOnlyMemory<byte> payload, EndPoint endpoint, CancellationToken token)
        {
            state = headers.Control == FlowControl.StreamEnd ? State.Final : State.InputExpected;
            var flushResult = await Writer.WriteAsync(payload, token).ConfigureAwait(false);
            return !flushResult.IsCanceled && !flushResult.IsCompleted;
        }

        public override ValueTask<(PacketHeaders, int, bool)> CreateOutboundMessageAsync(Memory<byte> payload, CancellationToken token)
        {
            (PacketHeaders Headers, int BytesWritten, bool WaitForInput) result = default;
            switch(state)
            {
                case State.Initial:
                    result.Headers = new PacketHeaders(MessageType.Metadata, FlowControl.None, CurrentTerm);
                    result.WaitForInput = true;
                    break;
                default:
                    result.Headers = new PacketHeaders(MessageType.Metadata, FlowControl.Ack, CurrentTerm);
                    result.WaitForInput = state == State.InputExpected;
                    break;
            }
            return new ValueTask<(PacketHeaders, int, bool)>(result);
        }
    }
}