using System.Buffers;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;

namespace DotNext.Net;

using Buffers;
using HttpEndPoint = Http.HttpEndPoint;

/// <summary>
/// Provides methods for serialization/deserialization of <see cref="EndPoint"/> derived types.
/// </summary>
/// <remarks>
/// List of supported endpoint types: <see cref="IPEndPoint"/>, <see cref="DnsEndPoint"/>,
/// <see cref="HttpEndPoint"/>, <see cref="UnixDomainSocketEndPoint"/>, <c>UriEndPoint</c>.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public static partial class EndPointFormatter
{
    /// <summary>
    /// Serializes endpoint address to the buffer.
    /// </summary>
    /// <param name="endPoint">The value to be serialized.</param>
    /// <param name="allocator">The buffer allocator.</param>
    /// <returns>The buffer containing serialized <paramref name="endPoint"/>.</returns>
    public static MemoryOwner<byte> GetBytes(this EndPoint endPoint, MemoryAllocator<byte>? allocator = null)
    {
        MemoryOwner<byte> result;
        var writer = new BufferWriterSlim<byte>(128, allocator);

        try
        {
            writer.WriteEndPoint(endPoint);

            result = writer.DetachOrCopyBuffer();
        }
        finally
        {
            writer.Dispose();
        }

        return result;
    }

    /// <summary>
    /// Serializes endpoint address to the buffer.
    /// </summary>
    /// <param name="writer">The output buffer.</param>
    /// <param name="endPoint">The value to be serialized.</param>
    /// <exception cref="ArgumentOutOfRangeException">Unsupported type of <paramref name="endPoint"/>.</exception>
    public static void WriteEndPoint(this ref BufferWriterSlim<byte> writer, EndPoint endPoint)
        => WriteEndPoint<BufferWriterSlim<byte>.Ref>(new(ref writer), endPoint);

    /// <summary>
    /// Serializes endpoint address to the buffer.
    /// </summary>
    /// <param name="writer">The output buffer.</param>
    /// <param name="endPoint">The value to be serialized.</param>
    /// <exception cref="ArgumentOutOfRangeException">Unsupported type of <paramref name="endPoint"/>.</exception>
    public static void WriteEndPoint(this IBufferWriter<byte> writer, EndPoint endPoint)
        => WriteEndPoint<IBufferWriter<byte>>(writer, endPoint);

    /// <summary>
    /// Deserializes endpoint address.
    /// </summary>
    /// <param name="reader">The binary reader.</param>
    /// <returns>The deserialized network endpoint address.</returns>
    public static EndPoint ReadEndPoint(this ref SequenceReader reader)
    {
        return reader.ReadByte() switch
        {
            IPEndPointPrefix => reader.ReadIp(),
            DnsEndPointPrefix => reader.ReadDns(),
            HttpEndPointPrefix => reader.ReadHttp(),
            DomainSocketEndPointPrefix => reader.ReadDomainSocket(),
            UriEndPointPrefix => reader.ReadUri(),
            _ => throw new NotSupportedException(),
        };
    }
}