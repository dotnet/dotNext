using System.Net;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.CustomTransport;

using Buffers;
using TransportServices;
using TransportServices.ConnectionOriented;

internal sealed class GenericClient : Client
{
    private sealed class GenericConnectionContext : Disposable, IConnectionContext
    {
        private readonly ConnectionContext transport;
        private readonly ProtocolPipeStream protocol;
        private MemoryOwner<byte> buffer;

        internal GenericConnectionContext(ConnectionContext context, MemoryAllocator<byte> defaultAllocator)
        {
            Debug.Assert(context is not null);

            var bufferSize = context.Transport.Output.GetSpan().Length;
            var allocator = context.Features.Get<MemoryAllocator<byte>>()
                ?? context.Features.Get<IMemoryPoolFeature>()?.MemoryPool?.ToAllocator()
                ?? defaultAllocator;
            buffer = allocator(bufferSize);
            transport = context;
            protocol = new(context.Transport, allocator, bufferSize);
        }

        Memory<byte> IConnectionContext.Buffer => buffer.Memory;

        ProtocolStream IConnectionContext.Protocol => protocol;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                protocol.Dispose();
                transport.Abort();
                if (transport is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                else
                {
                    using var task = transport.DisposeAsync().AsTask();
                    task.Wait();
                }
            }

            buffer.Dispose();
            base.Dispose(disposing);
        }

        protected override ValueTask DisposeAsyncCore()
        {
            protocol.Dispose();
            return transport.DisposeAsync();
        }

        public new ValueTask DisposeAsync() => base.DisposeAsync();
    }

    private readonly MemoryAllocator<byte> defaultAllocator;
    private readonly IConnectionFactory factory;

    internal GenericClient(ILocalMember localMember, EndPoint endPoint, IConnectionFactory factory, MemoryAllocator<byte> defaultAllocator)
        : base(localMember, endPoint)
    {
        Debug.Assert(factory is not null);
        Debug.Assert(defaultAllocator is not null);

        this.factory = factory;
        this.defaultAllocator = defaultAllocator;
    }

    private protected override async ValueTask<IConnectionContext> ConnectAsync(CancellationToken token)
    {
        // connection has separated timeout
        using var connectDurationTracker = CancellationTokenSource.CreateLinkedTokenSource(token);
        return new GenericConnectionContext(await factory.ConnectAsync(EndPoint, connectDurationTracker.Token).ConfigureAwait(false), defaultAllocator);
    }
}