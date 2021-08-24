using System;
using System.Buffers;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Discovery.HyParView.Http
{
    using Buffers;
    using IO;
    using IO.Pipelines;

    internal partial class HttpPeerController
    {
        private const string JoinMessageType = "Join";

        private async Task ProcessJoinAsync(HttpRequest request, HttpResponse response, long payloadLength, CancellationToken token)
        {
            EndPoint joinedPeer;
            if (request.BodyReader.TryReadBlock(payloadLength, out var result))
            {
                // fast path, no need to allocate temp buffer
                joinedPeer = DeserializeJoinRequest(result.Buffer, out var position);
                request.BodyReader.AdvanceTo(position);
            }
            else
            {
                // slow path, copy to temp buffer
                using var buffer = new PooledBufferWriter<byte>(allocator, payloadLength.Truncate());
                await request.BodyReader.CopyToAsync(buffer, token).ConfigureAwait(false);
                joinedPeer = DeserializeJoinRequest(buffer.WrittenMemory);
            }

            await EnqueueJoinAsync(joinedPeer, token).ConfigureAwait(false);
            response.StatusCode = StatusCodes.Status204NoContent;
        }

        private static EndPoint DeserializeJoinRequest(ReadOnlyMemory<byte> buffer)
        {
            var reader = IAsyncBinaryReader.Create(buffer);
            return reader.ReadEndPoint();
        }

        private static EndPoint DeserializeJoinRequest(ReadOnlySequence<byte> buffer, out SequencePosition position)
        {
            var reader = IAsyncBinaryReader.Create(buffer);
            var result = reader.ReadEndPoint();
            position = reader.Position;
            return result;
        }

        /// <inheritdoc/>
        protected sealed override async Task JoinAsync(EndPoint contactNode, CancellationToken token)
        {
            using var request = SerializeJoinRequest();
            await PostAsync(contactNode, JoinMessageType, request, token).ConfigureAwait(false);
        }

        private MemoryOwner<byte> SerializeJoinRequest()
        {
            Debug.Assert(localNode is not null);

            MemoryOwner<byte> result;
            var writer = new BufferWriterSlim<byte>(64, allocator);

            try
            {
                writer.WriteEndPoint(localNode);

                if (!writer.TryDetachBuffer(out result))
                    result = writer.WrittenSpan.Copy(allocator);
            }
            finally
            {
                writer.Dispose();
            }

            return result;
        }
    }
}