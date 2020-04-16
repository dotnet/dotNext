using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using static System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.Net.Cluster.Consensus.Raft.Tcp
{
    using TransportServices;
    using static IO.StreamExtensions;
    using ByteBuffer = Buffers.ArrayRental<byte>;

    internal abstract class TcpTransport : Disposable, INetworkTransport
    {
        private const int PacketPrologueSize = PacketHeaders.NaturalSize + sizeof(int);

        private protected class TcpStream : NetworkStream
        {
            internal TcpStream(Socket socket, bool owns)
                : base(socket, owns)
            {
            }

            private protected ValueTask WritePacket(PacketHeaders headers, Memory<byte> buffer, int count, CancellationToken token)
            {
                //write headers
                headers.WriteTo(buffer);
                WriteInt32LittleEndian(buffer.Span.Slice(PacketHeaders.NaturalSize), count);
                //transmit packet to the remote endpoint
                return WriteAsync(AdjustPacket(buffer, count), token);
            }

            private static void ReadPrologue(ReadOnlyMemory<byte> prologue, out PacketHeaders headers, out int count)
            {
                headers = new PacketHeaders(ref prologue);
                count = ReadInt32LittleEndian(prologue.Span);
            }

            private protected async ValueTask<(PacketHeaders Headers, ReadOnlyMemory<byte> Payload)> ReadPacket(Memory<byte> buffer, CancellationToken token)
            {
                //read headers and number of bytes
                await this.ReadBytesAsync(buffer.Slice(0, PacketPrologueSize), token).ConfigureAwait(false);
                ReadPrologue(buffer, out var headers, out var count);
                buffer = buffer.Slice(0, count);
                await this.ReadBytesAsync(buffer, token).ConfigureAwait(false);
                return (headers, buffer);
            }
        }

        private const int MinTransmissionBlockSize = 300;

        internal readonly IPEndPoint Address;
        private protected readonly ILogger logger;
        private readonly ArrayPool<byte> bufferPool;
        private int transmissionBlockSize;

        private protected TcpTransport(IPEndPoint address, ArrayPool<byte> pool, ILoggerFactory loggerFactory)
        {
            Address = address;
            logger = loggerFactory.CreateLogger(GetType());
            bufferPool = pool;
            transmissionBlockSize = MinTransmissionBlockSize;
            LingerOption = new LingerOption(false, 0);
        }

        private protected static void ConfigureSocket(Socket socket, LingerOption linger)
        {
            socket.NoDelay = true;
            socket.LingerState = linger;
        }

        internal static int ValidateTranmissionBlockSize(int value)
            => value >= MinTransmissionBlockSize ? value : throw new ArgumentOutOfRangeException(nameof(value));

        internal int TransmissionBlockSize
        {
            get => transmissionBlockSize;
            set => transmissionBlockSize = ValidateTranmissionBlockSize(value);
        }

        internal LingerOption LingerOption 
        { 
            get; 
            set;
        }

        IPEndPoint INetworkTransport.Address => Address;

        private protected ByteBuffer AllocTransmissionBlock()
            => new ByteBuffer(bufferPool, transmissionBlockSize);

        private protected static Memory<byte> AdjustToPayload(Memory<byte> packet)
            => packet.Slice(PacketPrologueSize);

        private protected static Memory<byte> AdjustPacket(Memory<byte> packet, int payloadSize)
            => packet.Slice(0, PacketPrologueSize + payloadSize);
    }
}