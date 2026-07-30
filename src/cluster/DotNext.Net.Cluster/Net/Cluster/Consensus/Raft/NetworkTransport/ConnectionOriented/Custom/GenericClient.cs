using System.Net;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.NetworkTransport.ConnectionOriented.Custom;

using Buffers;

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
                ?? context.Features.Get<IMemoryPoolFeature>()?.MemoryPool.ToAllocator()
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

    internal GenericClient(ILocalMember localMember, EndPoint endPoint)
        : base(localMember, endPoint)
    {
    }
    
    public required MemoryAllocator<byte> DefaultAllocator { get; init; }
    
    public required IConnectionFactory ConnectionFactory { get; init; }

    private protected override async ValueTask<IConnectionContext> ConnectAsync(CancellationToken token)
    {
        // connection has separated timeout
        var connectDurationTracker = CombineTokens(ConnectTimeout, token);
        ConnectionContext transport;
        try
        {
            transport = await ConnectionFactory.ConnectAsync(EndPoint, connectDurationTracker.Token).ConfigureAwait(false);
        }
        finally
        {
            await connectDurationTracker.DisposeAsync().ConfigureAwait(false);
        }

        return new GenericConnectionContext(transport, DefaultAllocator);
    }
}