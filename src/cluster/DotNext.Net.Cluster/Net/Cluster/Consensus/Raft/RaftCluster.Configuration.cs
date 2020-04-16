using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net;
using NullLoggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using TransportServices;
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
            private int? serverChannels;
            private int clientChannels;
            private ArrayPool<byte>? bufferPool;
            private ILoggerFactory? loggerFactory;
            private protected readonly Func<long> applicationIdGenerator;

            private protected NodeConfiguration(IPEndPoint hostAddress)
            {
                electionTimeout = ElectionTimeout.Recommended;
                heartbeatThreshold = 0.5D;
                Metadata = new Dictionary<string, string>();
                Members = new HashSet<IPEndPoint>();
                HostEndPoint = hostAddress;
                clientChannels = Environment.ProcessorCount + 1;
                applicationIdGenerator = new Random().Next<long>;
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
            /// Gets the address used for hosting of local member.
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
            /// node election timeout X threshold. The default is 0.5
            /// </remarks>
            /// <exception cref="ArgumentOutOfRangeException">Attempts to set invalid value.</exception>
            public double HeartbeatThreshold
            {
                get => heartbeatThreshold;
                set => heartbeatThreshold = value.Between(double.Epsilon, 1D, BoundType.Closed) ? value : throw new ArgumentOutOfRangeException(nameof(value));
            }

            /// <summary>
            /// Gets lower possible value of leader election timeout, in milliseconds.
            /// </summary>
            public int LowerElectionTimeout
            {
                get => electionTimeout.LowerValue;
                set => electionTimeout = electionTimeout.Modify(value, electionTimeout.UpperValue);
            }

            /// <summary>
            /// Gets upper possible value of leader election timeout, in milliseconds.
            /// </summary>
            public int UpperElectionTimeout
            {
                get => electionTimeout.UpperValue;
                set => electionTimeout = electionTimeout.Modify(electionTimeout.LowerValue, value);
            }

            /// <summary>
            /// Gets or sets configuration of the <see cref="Pipe"/> used for internal pipelined I/O.
            /// </summary>
            [AllowNull]
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
                get => serverChannels.GetValueOrDefault(Members.Count + 1);
                set => serverChannels = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
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
            /// Gets or sets buffer pool using for network I/O operations.
            /// </summary>
            [AllowNull]
            public ArrayPool<byte> BufferPool
            {
                get => bufferPool ?? ArrayPool<byte>.Shared;
                set => bufferPool = value;
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

            ElectionTimeout IClusterMemberConfiguration.ElectionTimeout => electionTimeout;

            /// <summary>
            /// Gets metadata associated with local cluster member.
            /// </summary>
            public IDictionary<string, string> Metadata { get; }

            /// <summary>
            /// Gets collection of cluster members.
            /// </summary>
            /// <value>The collection of cluster members.</value>
            public ICollection<IPEndPoint> Members { get; }

            private protected Func<int, ServerExchangePool> ExchangePoolFactory(ILocalMember localMember)
            {
                ServerExchangePool CreateExchangePool(int count)
                {
                    var result = new ServerExchangePool();
                    while (--count >= 0)
                        result.Add(new ServerExchange(localMember, PipeConfig));
                    return result;
                }
                return CreateExchangePool;
            }

            internal abstract RaftClusterMember CreateMemberClient(ILocalMember localMember, IPEndPoint endPoint, IClientMetricsCollector? metrics);

            internal abstract IServer CreateServer(ILocalMember localMember);
        }

        /// <summary>
        /// Represents configuration of the local cluster node that relies on UDP transport.
        /// </summary>
        public sealed class UdpConfiguration : NodeConfiguration
        {
            private int datagramSize;
            private bool dontFragment;

            /// <summary>
            /// Initializes a new UDP transport settings.
            /// </summary>
            /// <param name="localNodeHostAddress">The address used to listen requests to the local node.</param>
            public UdpConfiguration(IPEndPoint localNodeHostAddress)
                : base(localNodeHostAddress)
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

            private IClient CreateClient(IPEndPoint address)
                => new UdpClient(address, ClientBacklog, BufferPool, applicationIdGenerator, LoggerFactory) { DatagramSize = datagramSize, DontFragment = DontFragment };

            internal override RaftClusterMember CreateMemberClient(ILocalMember localMember, IPEndPoint endPoint, IClientMetricsCollector? metrics)
                => new ExchangePeer(localMember, endPoint, CreateClient, TimeSpan.FromMilliseconds(LowerElectionTimeout), PipeConfig, metrics);

            internal override IServer CreateServer(ILocalMember localMember)
                => new UdpServer(HostEndPoint, ServerBacklog, BufferPool, ExchangePoolFactory(localMember), LoggerFactory) { DatagramSize = datagramSize, DontFragment = DontFragment };
        }
    }
}