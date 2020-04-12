using System.Net;

namespace DotNext.Net.Cluster.Consensus.Raft.Udp
{
    using TransportServices;

    /// <summary>
    /// Represents UDP transport settings.
    /// </summary>
    public sealed class UdpTransport : TransportBinding
    {
        private int datagramSize;
        private bool dontFragment;

        /// <summary>
        /// Initializes a new UDP transport settings.
        /// </summary>
        public UdpTransport()
        {
            datagramSize = UdpSocket.MinDatagramSize;
        }

        /// <summary>
        /// Indicates that the IP datagrams can be fragmented.
        /// </summary>
        /// <remarks>Default value is <see langword="true"/>.</remarks>
        /// <seealso cref="DatagramSize"/>
        public bool DontFragment
        {
            get => dontFragment || datagramSize == UdpSocket.MinDatagramSize;
            set => dontFragment = value;
        }

        /// <summary>
        /// Gets or sets maximum datagram size, in bytes.
        /// </summary>
        /// <remarks>
        /// Make sure that datagram size matches to MTU if <see cref="DontFragment"/> is set;
        /// otherwise, UDP packets will be dropped.
        /// You can use <see cref="Net.NetworkInformation.MtuDiscovery"/> to discover allowed MTU size
        /// in your network and avoid fragmentation of packets.
        /// </remarks>
        public int DatagramSize
        {
            get => datagramSize;
            set => datagramSize = UdpSocket.ValidateDatagramSize(value);
        }

        internal override IClient CreateClient(IPEndPoint address)
            => new UdpClient(address, ClientBacklog, BufferPool, LoggerFactory) { DatagramSize = datagramSize, DontFragment = DontFragment };
    
        internal override IServer CreateServer(IPEndPoint address)
            => new UdpServer(address, ServerBacklog, BufferPool, LoggerFactory) { DatagramSize = datagramSize, DontFragment = DontFragment };
    }
}