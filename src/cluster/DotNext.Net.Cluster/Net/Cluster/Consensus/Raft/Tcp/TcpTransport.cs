using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using static System.Buffers.Binary.BinaryPrimitives;

namespace DotNext.Net.Cluster.Consensus.Raft.Tcp
{
    using Buffers;
    using TransportServices;
    using static IO.StreamExtensions;

    internal abstract class TcpTransport : Disposable, INetworkTransport
    {
        private const int PacketPrologueSize = PacketHeaders.NaturalSize + sizeof(int);

        private protected class TcpStream : NetworkStream
        {
            internal TcpStream(Socket socket, bool owns)
                : base(socket, owns)
            {
            }

            internal bool Connected => Socket.Connected;

            private protected ValueTask WritePacket(PacketHeaders headers, Memory<byte> buffer, int count, CancellationToken token)
            {
                // write headers
                headers.WriteTo(buffer);
                WriteInt32LittleEndian(buffer.Span.Slice(PacketHeaders.NaturalSize), count);

                // transmit packet to the remote endpoint
                return WriteAsync(AdjustPacket(buffer, count), token);
            }

            private static void ReadPrologue(ReadOnlyMemory<byte> prologue, out PacketHeaders headers, out int count)
            {
                headers = new PacketHeaders(ref prologue);
                count = (prologue.Span);
            }

            private protected async ValueTask<(PacketHeaders Headers, ReadOnlyMemory<byte> Payload)> ReadPacket(Memory<byte> buffer, CancellationToken token)
            {
                // read headers and number of bytes
                await this.ReadBlockAsync(buffer.Slice(0, PacketPrologueSize), token).ConfigureAwait(false);
                ReadPrologue(buffer, out var headers, out var count);
                buffer = buffer.Slice(0, count);
                await this.ReadBlockAsync(buffer, token).ConfigureAwait(false);
                return (headers, buffer);
            }
        }

        private const int MinTransmissionBlockSize = 300;

        internal readonly IPEndPoint Address;
        private protected readonly ILogger logger;
        private readonly MemoryAllocator<byte> allocator;
        private int transmissionBlockSize;

        private protected TcpTransport(IPEndPoint address, MemoryAllocator<byte> allocator, ILoggerFactory loggerFactory)
        {
            Address = address;
            logger = loggerFactory.CreateLogger(GetType());
            this.allocator = allocator;
            transmissionBlockSize = MinTransmissionBlockSize;
            LingerOption = new LingerOption(false, 0);
            Ttl = 64;
        }

        private protected static void ConfigureSocket(Socket socket, LingerOption linger, byte ttl)
        {
            socket.NoDelay = true;
            socket.Ttl = ttl;
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, linger);
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

        internal byte Ttl
        {
            get;
            set;
        }

        IPEndPoint INetworkTransport.Address => Address;

        private protected MemoryOwner<byte> AllocTransmissionBlock()
            => allocator(transmissionBlockSize);

        private protected static Memory<byte> AdjustToPayload(Memory<byte> packet)
            => packet.Slice(PacketPrologueSize);

        private protected static Memory<byte> AdjustPacket(Memory<byte> packet, int payloadSize)
            => packet.Slice(0, PacketPrologueSize + payloadSize);
    }
}