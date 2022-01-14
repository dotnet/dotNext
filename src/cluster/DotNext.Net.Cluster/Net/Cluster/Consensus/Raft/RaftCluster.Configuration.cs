using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net;
using Microsoft.Extensions.Logging;
using LingerOption = System.Net.Sockets.LingerOption;
using NullLoggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using Membership;
using Net.Security;
using Tcp;
using TransportServices;
using TransportServices.Datagram;
using Udp;
using IClientMetricsCollector = Metrics.IClientMetricsCollector;

public partial class RaftCluster
{
    /// <summary>
    /// Represents transport-agnostic configuration of cluster member.
    /// </summary>
    public abstract class NodeConfiguration : IClusterMemberConfiguration
    {
        private double heartbeatThreshold;
        private ElectionTimeout electionTimeout;
        private IPEndPoint? publicAddress;
        private PipeOptions? pipeConfig;
        private MemoryAllocator<byte>? allocator;
        private int serverChannels = 10;
        private ILoggerFactory? loggerFactory;
        private TimeSpan? requestTimeout;
        private int warmupRounds;
        private protected readonly Func<long> applicationIdGenerator;

        private protected NodeConfiguration(IPEndPoint hostAddress)
        {
            electionTimeout = Raft.ElectionTimeout.Recommended;
            heartbeatThreshold = 0.5D;
            Metadata = new Dictionary<string, string>();
            HostEndPoint = hostAddress;
            applicationIdGenerator = Random.Shared.Next<long>;
            TimeToLive = 64;
            warmupRounds = 10;
        }

        /// <summary>
        /// Gets or sets metrics collector.
        /// </summary>
        public MetricsCollector? Metrics
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the address used for hosting local member.
        /// </summary>
        public IPEndPoint HostEndPoint { get; }

        /// <summary>
        /// Gets the address of the local member visible to other members.
        /// </summary>
        /// <remarks>
        /// This property is useful when local member hosted in a container (Windows, LXC or Docker)
        /// because <see cref="HostEndPoint"/> may return <see cref="IPAddress.Any"/> or
        /// <see cref="IPAddress.IPv6Any"/>.
        /// </remarks>
        [AllowNull]
        public IPEndPoint PublicEndPoint
        {
            get => publicAddress ?? HostEndPoint;
            set => publicAddress = value;
        }

        /// <summary>
        /// Gets or sets the storage for cluster configuration.
        /// </summary>
        /// <remarks>
        /// If not set then use in-memory storage by default.
        /// </remarks>
        public IClusterConfigurationStorage<IPEndPoint>? ConfigurationStorage { get; set; }

        /// <summary>
        /// Sets <see cref="ConfigurationStorage"/> to in-memory configuration storage.
        /// </summary>
        /// <remarks>
        /// This storage is not recommended for production use.
        /// </remarks>
        /// <returns>The constructed storage.</returns>
        public InMemoryClusterConfigurationStorage<IPEndPoint> UseInMemoryConfigurationStorage()
        {
            var storage = new InMemoryClusterConfigurationStorage(MemoryAllocator);
            ConfigurationStorage = storage;
            return storage;
        }

        /// <summary>
        /// Indicates that each part of cluster in partitioned network allow to elect its own leader.
        /// </summary>
        /// <remarks>
        /// <see langword="false"/> value allows to build CA distributed cluster
        /// while <see langword="true"/> value allows to build CP/AP distributed cluster.
        /// </remarks>
        public bool Partitioning { get; set; }

        /// <summary>
        /// Gets or sets threshold of the heartbeat timeout.
        /// </summary>
        /// <remarks>
        /// The threshold should be in range (0, 1). The heartbeat timeout is computed as
        /// node election timeout X threshold. The default is 0.5.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Attempts to set invalid value.</exception>
        public double HeartbeatThreshold
        {
            get => heartbeatThreshold;
            set => heartbeatThreshold = value.IsBetween(double.Epsilon, 1D, BoundType.Closed) ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }

        /// <summary>
        /// Gets lower possible value of leader election timeout, in milliseconds.
        /// </summary>
        public int LowerElectionTimeout
        {
            get => electionTimeout.LowerValue;
            set => electionTimeout = electionTimeout with { LowerValue = value };
        }

        /// <summary>
        /// Gets or sets memory allocator to be used for network I/O.
        /// </summary>
        [AllowNull]
        public MemoryAllocator<byte> MemoryAllocator
        {
            get => allocator ??= pipeConfig?.Pool.ToAllocator() ?? ArrayPool<byte>.Shared.ToAllocator();
            set => allocator = value;
        }

        /// <summary>
        /// Gets or sets the delegate that can be used to announce the node to the cluster
        /// if <see cref="ColdStart"/> is <see langword="false"/>.
        /// </summary>
        public ClusterMemberAnnouncer<IPEndPoint>? Announcer
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the numbers of rounds used to warmup a fresh node which wants to join the cluster.
        /// </summary>
        public int WarmupRounds
        {
            get => warmupRounds;
            set => warmupRounds = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(warmupRounds));
        }

        /// <summary>
        /// Gets or sets a value indicating that the initial node in the cluster is starting.
        /// </summary>
        public bool ColdStart { get; set; } = true;

        /// <summary>
        /// Gets or sets request processing timeout.
        /// </summary>
        public TimeSpan RequestTimeout
        {
            get => requestTimeout ?? TimeSpan.FromMilliseconds(LowerElectionTimeout);
            set => requestTimeout = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }

        /// <summary>
        /// Gets upper possible value of leader election timeout, in milliseconds.
        /// </summary>
        public int UpperElectionTimeout
        {
            get => electionTimeout.UpperValue;
            set => electionTimeout = electionTimeout with { UpperValue = value };
        }

        /// <summary>
        /// Gets or sets configuration of the <see cref="Pipe"/> used for internal pipelined I/O.
        /// </summary>
        [AllowNull]
        [Obsolete("I/O pipe configuration is application to UDP transport only. Use UdpConfiguration.PipeConfig instead", true)]
        public PipeOptions PipeConfig
        {
            get => pipeConfig ?? PipeOptions.Default;
            set => pipeConfig = value;
        }

        /// <summary>
        /// Gets or sets the maximum number of parallel requests that can be handled simultaneously.
        /// </summary>
        /// <remarks>
        /// By default, the value based on the number of cluster members.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Supplied value is equal to or less than zero.</exception>
        public int ServerBacklog
        {
            get => serverChannels;
            set => serverChannels = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }

        /// <summary>
        /// Gets or sets a value that specifies the Time To Live (TTL) value of Internet Protocol (IP) packets.
        /// </summary>
        public byte TimeToLive
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets logger factory using for internal logging.
        /// </summary>
        [AllowNull]
        [CLSCompliant(false)]
        public ILoggerFactory LoggerFactory
        {
            get => loggerFactory ?? NullLoggerFactory.Instance;
            set => loggerFactory = value;
        }

        /// <inheritdoc/>
        ElectionTimeout IClusterMemberConfiguration.ElectionTimeout => electionTimeout;

        /// <summary>
        /// Gets metadata associated with local cluster member.
        /// </summary>
        public IDictionary<string, string> Metadata { get; }

        /// <summary>
        /// Gets or sets a value indicating that the cluster member
        /// represents standby node which is never become a leader.
        /// </summary>
        public bool Standby { get; set; }

        internal abstract RaftClusterMember CreateMemberClient(ILocalMember localMember, IPEndPoint endPoint, ClusterMemberId id, IClientMetricsCollector? metrics);

        internal abstract IServer CreateServer(ILocalMember localMember);
    }

    /// <summary>
    /// Represents configuration of the local cluster node that relies on UDP transport.
    /// </summary>
    public sealed class UdpConfiguration : NodeConfiguration
    {
        private static readonly IPEndPoint DefaultLocalEndPoint = new(IPAddress.Any, 0);

        private int clientChannels;
        private int datagramSize;
        private bool dontFragment;
        private PipeOptions? pipeConfig;

        /// <summary>
        /// Initializes a new UDP transport settings.
        /// </summary>
        /// <param name="localNodeHostAddress">The address used to listen requests to the local node.</param>
        public UdpConfiguration(IPEndPoint localNodeHostAddress)
            : base(localNodeHostAddress)
        {
            datagramSize = UdpSocket.MinDatagramSize;
            clientChannels = Environment.ProcessorCount + 1;
            LocalEndPoint = DefaultLocalEndPoint;
        }

        /// <summary>
        /// Gets or sets the maximum number of parallel requests that can be initiated by the client.
        /// </summary>
        public int ClientBacklog
        {
            get => clientChannels;
            set => clientChannels = value > 1 ? value : throw new ArgumentOutOfRangeException(nameof(value));
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

        /// <summary>
        /// Gets or sets local network interface to be used for receiving UDP packets
        /// by Raft cluster member clients.
        /// </summary>
        /// <remarks>
        /// This endpoint is not used for initialization of server, only for clients.
        /// UDP is connectionless protocol and underlying implementation must know
        /// which interface should be used for receiving responses from server through UDP
        /// transport. By default, this property listens on all network interfaces and using
        /// randomly selected port. For most situations, it's redundant and unsafe.
        /// </remarks>
        public IPEndPoint LocalEndPoint { get; set; }

        /// <summary>
        /// Gets or sets configuration of the <see cref="Pipe"/> used for internal pipelined I/O.
        /// </summary>
        [AllowNull]
        public new PipeOptions PipeConfig
        {
            get => pipeConfig ?? PipeOptions.Default;
            set => pipeConfig = value;
        }

        private UdpClient CreateClient(IPEndPoint address)
            => new(LocalEndPoint, address, ClientBacklog, MemoryAllocator, applicationIdGenerator, LoggerFactory)
            {
                DatagramSize = datagramSize,
                DontFragment = DontFragment,
                Ttl = TimeToLive,
            };

        private Func<int, ExchangePool> ExchangePoolFactory(ILocalMember localMember)
        {
            return CreateExchangePool;

            ExchangePool CreateExchangePool(int count)
            {
                var result = new ExchangePool();
                while (--count >= 0)
                    result.Add(new ServerExchange(localMember, PipeConfig));
                return result;
            }
        }

        internal override ExchangePeer CreateMemberClient(ILocalMember localMember, IPEndPoint endPoint, ClusterMemberId id, IClientMetricsCollector? metrics)
            => new(localMember, endPoint, id, CreateClient)
            {
                RequestTimeout = RequestTimeout,
                Metrics = metrics,
                PipeConfig = PipeConfig,
            };

        internal override UdpServer CreateServer(ILocalMember localMember)
            => new(HostEndPoint, ServerBacklog, MemoryAllocator, ExchangePoolFactory(localMember), LoggerFactory)
            {
                DatagramSize = datagramSize,
                DontFragment = DontFragment,
                ReceiveTimeout = RequestTimeout,
                Ttl = TimeToLive,
            };
    }

    /// <summary>
    /// Represents configuration of the local cluster node that relies on TCP transport.
    /// </summary>
    public sealed class TcpConfiguration : NodeConfiguration
    {
        private int transmissionBlockSize, connectTimeout;
        private TimeSpan? gracefulShutdown;

        /// <summary>
        /// Initializes a new UDP transport settings.
        /// </summary>
        /// <param name="localNodeHostAddress">The address used to listen requests to the local node.</param>
        public TcpConfiguration(IPEndPoint localNodeHostAddress)
            : base(localNodeHostAddress)
        {
            transmissionBlockSize = ushort.MaxValue;
            connectTimeout = int.MinValue;
            LingerOption = ITcpTransport.CreateDefaultLingerOption();
        }

        /// <summary>
        /// Gets configuration that specifies whether a TCP socket will
        /// delay its closing in an attempt to send all pending data.
        /// </summary>
        public LingerOption LingerOption
        {
            get;
        }

        /// <summary>
        /// Gets or sets timeout used for graceful shtudown of the server.
        /// </summary>
        public TimeSpan GracefulShutdownTimeout
        {
            get => gracefulShutdown ?? RequestTimeout;
            set => gracefulShutdown = value;
        }

        /// <summary>
        /// Gets or sets the size of logical block that can be transmitted
        /// to the remote host without requesting the next block.
        /// </summary>
        /// <remarks>
        /// This property allows to reduce signal traffic between endpoints
        /// and affects performance of <see cref="IRaftClusterMember.AppendEntriesAsync{TEntry, TList}"/>,
        /// <see cref="IRaftClusterMember.InstallSnapshotAsync"/> and
        /// <see cref="IClusterMember.GetMetadataAsync"/> methods. Ideally, the value
        /// must be equal to average size of single log entry.
        /// </remarks>
        public int TransmissionBlockSize
        {
            get => transmissionBlockSize;
            set => transmissionBlockSize = TcpTransport.ValidateTranmissionBlockSize(value);
        }

        /// <summary>
        /// Gets or sets transport-level encryption options.
        /// </summary>
        /// <value><see langword="null"/> to disable transport-level encryption.</value>
        public SslOptions? SslOptions
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets TCP connection timeout, in milliseconds.
        /// </summary>
        [Obsolete("ConnectTimeout is no longer in use. RequestTimeout is used for connection instead")]
        public int ConnectTimeout
        {
            get => connectTimeout > 0 ? connectTimeout : (LowerElectionTimeout / 2);
            set => connectTimeout = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }

        internal override TcpClient CreateMemberClient(ILocalMember localMember, IPEndPoint endPoint, ClusterMemberId id, IClientMetricsCollector? metrics)
            => new(localMember, endPoint, id, MemoryAllocator)
            {
                TransmissionBlockSize = TransmissionBlockSize,
                LingerOption = LingerOption,
                Ttl = TimeToLive,
                SslOptions = SslOptions?.ClientOptions,
                RequestTimeout = RequestTimeout,
                Metrics = metrics,
            };

        internal override TcpServer CreateServer(ILocalMember localMember)
            => new(HostEndPoint, ServerBacklog, localMember, MemoryAllocator, LoggerFactory)
            {
                TransmissionBlockSize = TransmissionBlockSize,
                LingerOption = LingerOption,
                ReceiveTimeout = RequestTimeout,
                GracefulShutdownTimeout = (int)GracefulShutdownTimeout.TotalMilliseconds,
                Ttl = TimeToLive,
                SslOptions = SslOptions?.ServerOptions,
            };
    }
}