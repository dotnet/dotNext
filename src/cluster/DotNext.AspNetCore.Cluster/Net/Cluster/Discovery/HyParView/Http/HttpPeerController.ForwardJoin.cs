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
        private const string ForwardJoinMessageType = "ForwardJoin";

        private async Task ProcessForwardJoinAsync(HttpRequest request, HttpResponse response, int payloadLength, CancellationToken token)
        {
            EndPoint sender, joinedPeer;
            int timeToLive;

            if (request.BodyReader.TryReadBlock(payloadLength, out var result))
            {
                // fast path, no need to allocate temp buffer
                (sender, joinedPeer, timeToLive) = DeserializeForwardJoinRequest(result.Buffer, out var position);
                request.BodyReader.AdvanceTo(position);
            }
            else
            {
                // slow path, allocate temp buffer
                using var buffer = allocator.Invoke(payloadLength, true);
                await request.BodyReader.ReadBlockAsync(buffer.Memory, token).ConfigureAwait(false);
                (sender, joinedPeer, timeToLive) = DeserializeForwardJoinRequest(buffer.Memory);
            }

            await EnqueueForwardJoinAsync(sender, joinedPeer, timeToLive, token).ConfigureAwait(false);
            response.StatusCode = StatusCodes.Status204NoContent;
        }

        private static (EndPoint, EndPoint, int) DeserializeForwardJoinRequest(ref SequenceBinaryReader reader)
            => (reader.ReadEndPoint(), reader.ReadEndPoint(), reader.ReadInt32(true));

        private static (EndPoint, EndPoint, int) DeserializeForwardJoinRequest(ReadOnlyMemory<byte> content)
        {
            var reader = IAsyncBinaryReader.Create(content);
            return DeserializeForwardJoinRequest(ref reader);
        }

        private static (EndPoint, EndPoint, int) DeserializeForwardJoinRequest(ReadOnlySequence<byte> content, out SequencePosition position)
        {
            var reader = IAsyncBinaryReader.Create(content);
            var result = DeserializeForwardJoinRequest(ref reader);
            position = reader.Position;
            return result;
        }

        protected sealed override async Task ForwardJoinAsync(EndPoint receiver, EndPoint joinedPeer, int timeToLive, CancellationToken token = default)
        {
            using var request = SerializeForwardJoinRequest(joinedPeer, timeToLive);
            await PostAsync(receiver, ForwardJoinMessageType, request, token).ConfigureAwait(false);
        }

        private MemoryOwner<byte> SerializeForwardJoinRequest(EndPoint joinedPeer, int timeToLive)
        {
            Debug.Assert(localNode is not null);

            MemoryOwner<byte> result;
            var writer = new BufferWriterSlim<byte>(128, allocator);

            try
            {
                writer.WriteEndPoint(localNode);
                writer.WriteEndPoint(joinedPeer);
                writer.WriteInt32(timeToLive, true);

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