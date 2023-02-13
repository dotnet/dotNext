using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.Datagram;

internal interface IClientExchange<out T> : ISupplier<CancellationToken, T>, IExchange
    where T : Task
{
    ClusterMemberId Sender { set; }

    string Name { get; }
}

internal abstract class ClientExchange<T> : TaskCompletionSource<T>, IClientExchange<Task<T>>
{
    private readonly string name;
    private protected ClusterMemberId sender;

    private protected ClientExchange(string name)
        : base(TaskCreationOptions.RunContinuationsAsynchronously)
    {
        Debug.Assert(name is { Length: > 0 });

        this.name = name;
    }

    string IClientExchange<Task<T>>.Name => name;

    Task<T> ISupplier<CancellationToken, Task<T>>.Invoke(CancellationToken token) => Task;

    ClusterMemberId IClientExchange<Task<T>>.Sender
    {
        set => sender = value;
    }

    public abstract ValueTask<bool> ProcessInboundMessageAsync(PacketHeaders headers, ReadOnlyMemory<byte> payload, CancellationToken token);

    public abstract ValueTask<(PacketHeaders Headers, int BytesWritten, bool)> CreateOutboundMessageAsync(Memory<byte> payload, CancellationToken token);

    private protected virtual void OnException(Exception e)
    {
    }

    void IExchange.OnException(Exception e)
    {
        if (e is OperationCanceledException cancellation ? TrySetCanceled(cancellation.CancellationToken) : TrySetException(e))
            OnException(e);
    }

    private protected virtual void OnCanceled(CancellationToken token)
    {
    }

    void IExchange.OnCanceled(CancellationToken token)
    {
        if (TrySetCanceled(token))
            OnCanceled(token);
    }
}

internal abstract class ClientExchange : ClientExchange<Result<bool>>
{
    private protected readonly long currentTerm;

    private protected ClientExchange(string name, long term)
        : base(name) => currentTerm = term;

    public sealed override ValueTask<bool> ProcessInboundMessageAsync(PacketHeaders headers, ReadOnlyMemory<byte> payload, CancellationToken token)
    {
        Debug.Assert(headers.Control == FlowControl.Ack, "Unexpected response", $"Message type {headers.Type} control {headers.Control}");
        TrySetResult(Result.Read(payload.Span));
        return new(false);
    }
}