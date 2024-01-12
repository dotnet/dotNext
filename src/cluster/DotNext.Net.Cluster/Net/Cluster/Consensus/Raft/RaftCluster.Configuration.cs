using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net;
using System.Runtime.Versioning;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using LingerOption = System.Net.Sockets.LingerOption;
using NullLoggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using CustomTransport;
using Membership;
using Net.Security;
using Tcp;
using TransportServices;

public partial class RaftCluster
{
    /// <summary>
    /// Represents transport-agnostic configuration of cluster member.
    /// </summary>
    public abstract class NodeConfiguration : IClusterMemberConfiguration
    {
        private double heartbeatThreshold;
        private ElectionTimeout electionTimeout;
        private MemoryAllocator<byte>? allocator;
        private ILoggerFactory? loggerFactory;
        private TimeSpan? requestTimeout;
        private int warmupRounds;
        private EndPoint? publicAddress;

        private protected NodeConfiguration()
        {
            electionTimeout = Raft.ElectionTimeout.Recommended;
            heartbeatThreshold = 0.5D;
            Metadata = new Dictionary<string, string>();
            warmupRounds = 10;
        }

        /// <summary>
        /// Gets the address used for hosting local member.
        /// </summary>
        public abstract EndPoint HostEndPoint { get; }

        /// <summary>
        /// Gets the address of the local member visible to other members.
        /// </summary>
        [AllowNull]
        public EndPoint PublicEndPoint
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
        public IClusterConfigurationStorage<EndPoint>? ConfigurationStorage { get; set; }

        /// <summary>
        /// Sets <see cref="ConfigurationStorage"/> to in-memory configuration storage.
        /// </summary>
        /// <remarks>
        /// This storage is not recommended for production use.
        /// </remarks>
        /// <returns>The constructed storage.</returns>
        public InMemoryClusterConfigurationStorage<EndPoint> UseInMemoryConfigurationStorage()
        {
            var storage = new InMemoryClusterConfigurationStorage(this.As<IClusterMemberConfiguration>().EndPointComparer, MemoryAllocator);
            ConfigurationStorage = storage;
            return storage;
        }

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
            set => heartbeatThreshold = value is > 0D and < 1D ? value : throw new ArgumentOutOfRangeException(nameof(value));
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
            get => allocator ??= ArrayPool<byte>.Shared.ToAllocator();
            set => allocator = value;
        }

        /// <summary>
        /// Gets or sets the delegate that can be used to announce the node to the cluster
        /// if <see cref="ColdStart"/> is <see langword="false"/>.
        /// </summary>
        public ClusterMemberAnnouncer<EndPoint>? Announcer
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

        /// <summary>
        /// Gets a value indicating that the follower node should not try to upgrade
        /// to the candidate state if the leader is reachable via the network.
        /// </summary>
        public bool AggressiveLeaderStickiness { get; set; }

        internal abstract RaftClusterMember CreateClient(ILocalMember localMember, EndPoint endPoint);

        internal abstract IServer CreateServer(ILocalMember localMember);
    }

    private interface IConnectionOrientedTransportConfiguration : IClusterMemberConfiguration
    {
        TimeSpan ConnectTimeout { get; set; }
    }

    /// <summary>
    /// Provides configuration of cluster node whose communication is based on custom network transport.
    /// </summary>
    [CLSCompliant(false)]
    public sealed class CustomTransportConfiguration : NodeConfiguration, IConnectionOrientedTransportConfiguration
    {
        private readonly IConnectionListenerFactory serverFactory;
        private readonly IConnectionFactory clientFactory;
        private readonly IEqualityComparer<EndPoint>? endPointComparer;
        private TimeSpan? connectTimeout;

        /// <summary>
        /// Initializes a new custom transport settings.
        /// </summary>
        /// <param name="localNodeHostAddress">The address used to listen requests to the local node.</param>
        /// <param name="serverConnFactory">The connection factory that is used to listen incoming connections.</param>
        /// <param name="clientConnFactory">The connection factory that is used to produce outbound connections.</param>
        /// <exception cref="ArgumentNullException"><paramref name="localNodeHostAddress"/> or <paramref name="serverConnFactory"/> or <paramref name="clientConnFactory"/> is <see langword="null"/>.</exception>
        public CustomTransportConfiguration(EndPoint localNodeHostAddress, IConnectionListenerFactory serverConnFactory, IConnectionFactory clientConnFactory)
        {
            ArgumentNullException.ThrowIfNull(localNodeHostAddress);
            ArgumentNullException.ThrowIfNull(serverConnFactory);
            ArgumentNullException.ThrowIfNull(clientConnFactory);

            serverFactory = serverConnFactory;
            clientFactory = clientConnFactory;
            HostEndPoint = localNodeHostAddress;
        }

        /// <summary>
        /// Gets or sets TCP connection timeout, in milliseconds.
        /// </summary>
        public TimeSpan ConnectTimeout
        {
            get => connectTimeout ?? RequestTimeout;
            set => connectTimeout = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }

        /// <inheritdoc />
        public override EndPoint HostEndPoint { get; }

        /// <summary>
        /// Gets or sets a comparer for <see cref="EndPoint"/> data type.
        /// </summary>
        [AllowNull]
        public IEqualityComparer<EndPoint> EndPointComparer
        {
            get => endPointComparer ?? EqualityComparer<EndPoint>.Default;
            init => endPointComparer = value;
        }

        /// <inheritdoc />
        IEqualityComparer<EndPoint> IClusterMemberConfiguration.EndPointComparer => EndPointComparer;

        internal override GenericClient CreateClient(ILocalMember localMember, EndPoint endPoint)
            => new(localMember, endPoint, clientFactory, MemoryAllocator) { ConnectTimeout = ConnectTimeout };

        internal override GenericServer CreateServer(ILocalMember localMember)
            => new(HostEndPoint, serverFactory, localMember, MemoryAllocator, LoggerFactory) { ReceiveTimeout = RequestTimeout };
    }

    /// <summary>
    /// Provides configuration of cluster node whose communication is based on network
    /// transport implemented by .NEXT library.
    /// </summary>
    public abstract class BuiltInTransportConfiguration : NodeConfiguration
    {
        private int serverChannels = 10;

        private protected BuiltInTransportConfiguration(IPEndPoint hostAddress)
        {
            HostEndPoint = hostAddress ?? throw new ArgumentNullException(nameof(hostAddress));
            TimeToLive = 64;
        }

        /// <inheritdoc />
        public sealed override IPEndPoint HostEndPoint { get; }

        /// <summary>
        /// Gets or sets the maximum number of parallel requests that can be handled simultaneously.
        /// </summary>
        /// <remarks>
        /// By default, it is 10.
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
    }

    /// <summary>
    /// Represents configuration of the local cluster node that relies on TCP transport.
    /// </summary>
    public sealed class TcpConfiguration : BuiltInTransportConfiguration, IConnectionOrientedTransportConfiguration
    {
        private int transmissionBlockSize;
        private TimeSpan? gracefulShutdown, connectTimeout;

        /// <summary>
        /// Initializes a new UDP transport settings.
        /// </summary>
        /// <param name="localNodeHostAddress">The address used to listen requests to the local node.</param>
        public TcpConfiguration(IPEndPoint localNodeHostAddress)
            : base(localNodeHostAddress)
        {
            transmissionBlockSize = ushort.MaxValue;
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
        /// Gets or sets timeout used for graceful shutdown of the server.
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
            set => transmissionBlockSize = ITcpTransport.ValidateTransmissionBlockSize(value);
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
        public TimeSpan ConnectTimeout
        {
            get => connectTimeout ?? RequestTimeout;
            set => connectTimeout = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }

        internal override TcpClient CreateClient(ILocalMember localMember, EndPoint endPoint)
            => new(localMember, endPoint, MemoryAllocator)
            {
                TransmissionBlockSize = TransmissionBlockSize,
                LingerOption = LingerOption,
                Ttl = TimeToLive,
                SslOptions = SslOptions?.ClientOptions,
                RequestTimeout = RequestTimeout,
                ConnectTimeout = ConnectTimeout,
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