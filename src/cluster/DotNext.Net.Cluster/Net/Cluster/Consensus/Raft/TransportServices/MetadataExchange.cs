using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    using Text;
    using static IO.Pipelines.PipeExtensions;
    using static IO.Pipelines.ResultExtensions;
    using LengthFormat = IO.LengthFormat;

    internal sealed class MetadataExchange : PipeExchange, IClientExchange<IReadOnlyDictionary<string, string>>
    {
        private const LengthFormat LengthEncoding = LengthFormat.Compressed;

        private readonly CancellationToken readToken;
        private bool state;

        internal MetadataExchange(CancellationToken token, PipeOptions? options = null)
            : base(options) => readToken = token;

        private static Encoding Encoding => Encoding.UTF8;

        // port announcement is not used for this request
        ushort IClientExchange.MyPort
        {
            set { }
        }

        internal static async Task WriteAsync(PipeWriter writer, IReadOnlyDictionary<string, string> input, CancellationToken token)
        {
            // write length
            var flushResult = await writer.WriteInt32Async(input.Count, true, token).ConfigureAwait(false);
            if (flushResult.IsCompleted)
                return;
            flushResult.ThrowIfCancellationRequested(token);

            // write pairs
            var context = new EncodingContext(Encoding, true);
            foreach (var (key, value) in input)
            {
                await writer.WriteStringAsync(key.AsMemory(), context, lengthFormat: LengthEncoding, token: token).ConfigureAwait(false);
                await writer.WriteStringAsync(value.AsMemory(), context, lengthFormat: LengthEncoding, token: token).ConfigureAwait(false);
            }

            await writer.CompleteAsync().ConfigureAwait(false);
        }

        private static async Task<IReadOnlyDictionary<string, string>> ReadAsync(PipeReader reader, CancellationToken token)
        {
            // read length
            var length = await reader.ReadInt32Async(true, token).ConfigureAwait(false);
            var output = new Dictionary<string, string>(length, StringComparer.Ordinal);
            var context = new DecodingContext(Encoding, true);
            while (--length >= 0)
            {
                // read key
                var key = await reader.ReadStringAsync(LengthEncoding, context, token).ConfigureAwait(false);

                // read value
                var value = await reader.ReadStringAsync(LengthEncoding, context, token).ConfigureAwait(false);

                // write pair to the dictionary
                output.Add(key, value);
            }

            return output;
        }

        public Task<IReadOnlyDictionary<string, string>> Task => ReadAsync(Reader, readToken);

        public override async ValueTask<bool> ProcessInboundMessageAsync(PacketHeaders headers, ReadOnlyMemory<byte> payload, EndPoint endpoint, CancellationToken token)
        {
            var flushResult = await Writer.WriteAsync(payload, token).ConfigureAwait(false);
            return !(flushResult.IsCanceled | flushResult.IsCompleted | headers.Control == FlowControl.StreamEnd);
        }

        public override ValueTask<(PacketHeaders, int, bool)> CreateOutboundMessageAsync(Memory<byte> payload, CancellationToken token)
        {
            FlowControl control;
            if (state)
            {
                control = FlowControl.Ack;
            }
            else
            {
                state = true;
                control = FlowControl.None;
            }

            return new ValueTask<(PacketHeaders, int, bool)>((new PacketHeaders(MessageType.Metadata, control), 0, true));
        }
    }
}