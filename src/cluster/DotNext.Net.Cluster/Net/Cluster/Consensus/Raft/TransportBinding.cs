using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using TransportServices;
    using NullLoggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory;

    /// <summary>
    /// Represents settings of network transport for Raft protocol.
    /// </summary>
    public abstract class TransportBinding
    {
        private int? serverChannels;
        private int clientChannels;
        private ArrayPool<byte>? bufferPool;
        private ILoggerFactory? loggerFactory;

        private protected TransportBinding()
        {
            clientChannels = Environment.ProcessorCount + 1;
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
            get => serverChannels.GetValueOrDefault(Environment.ProcessorCount);
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

        internal bool UseDefaultServerChannels => serverChannels is null;

        internal abstract IClient CreateClient(IPEndPoint address);

        internal abstract IServer CreateServer(IPEndPoint address);
    }
}