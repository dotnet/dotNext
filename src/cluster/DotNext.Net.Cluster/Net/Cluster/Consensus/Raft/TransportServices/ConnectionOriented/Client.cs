using System.Net;
using System.Runtime.CompilerServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using Threading;
using Timestamp = Diagnostics.Timestamp;
using IClusterConfiguration = Membership.IClusterConfiguration;

internal abstract partial class Client : RaftClusterMember
{
    private protected interface IConnectionContext : IDisposable, IAsyncDisposable
    {
        ProtocolStream Protocol { get; }

        Memory<byte> Buffer { get; }
    }

    // combine request/reply state machine with request arguments to reduce multiple allocations
    // and boxing operation that will happen on every request
    private abstract class Request<TResponse> : IAsyncStateMachine
    {
        private const uint InitialState = 0U;
        private const uint RequestState = 1U;
        private uint state;
        private ProtocolStream? protocol;
        private Memory<byte> buffer;
        private CancellationToken token;
        private AsyncValueTaskMethodBuilder<TResponse> builder;
        private ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter requestAwaiter;
        private ConfiguredValueTaskAwaitable<TResponse>.ConfiguredValueTaskAwaiter responseAwaiter;

        void IAsyncStateMachine.MoveNext()
        {
            try
            {
                MoveNext(this);
            }
            catch (Exception e)
            {
                builder.SetException(e);
                Cleanup();
            }
        }

        private static void MoveNext(Request<TResponse> stateMachine)
        {
            Debug.Assert(stateMachine.protocol is not null);

            switch (stateMachine.state)
            {
                case InitialState:
                    stateMachine.requestAwaiter = stateMachine.GetRequestAwaiter();
                    stateMachine.state = RequestState;
                    if (stateMachine.requestAwaiter.IsCompleted)
                        goto case RequestState;
                    stateMachine.builder.AwaitOnCompleted(ref stateMachine.requestAwaiter, ref stateMachine);
                    break;
                case RequestState:
                    GetResultAndClear(ref stateMachine.requestAwaiter);
                    stateMachine.protocol.Reset(); // prepare stream to read response

                    stateMachine.responseAwaiter = stateMachine.GetResponseAwaiter();
                    stateMachine.state = RequestState + 1U;
                    if (stateMachine.responseAwaiter.IsCompleted)
                        goto case default;
                    stateMachine.builder.AwaitOnCompleted(ref stateMachine.responseAwaiter, ref stateMachine);
                    break;
                default:
                    stateMachine.builder.SetResult(stateMachine.responseAwaiter.GetResult());
                    stateMachine.Cleanup();
                    break;
            }

            static void GetResultAndClear(ref ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter awaiter)
            {
                awaiter.GetResult();
                awaiter = default;
            }
        }

        private void Cleanup()
        {
            protocol = default;
            buffer = default;
            token = default;
            requestAwaiter = default;
            responseAwaiter = default;
        }

        private ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter GetRequestAwaiter()
        {
            Debug.Assert(protocol is not null);

            return RequestAsync(protocol, buffer, token).ConfigureAwait(false).GetAwaiter();
        }

        private protected abstract ValueTask RequestAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token);

        private ConfiguredValueTaskAwaitable<TResponse>.ConfiguredValueTaskAwaiter GetResponseAwaiter()
        {
            Debug.Assert(protocol is not null);

            return ResponseAsync(protocol, buffer, token).ConfigureAwait(false).GetAwaiter();
        }

        private protected abstract ValueTask<TResponse> ResponseAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token);

        void IAsyncStateMachine.SetStateMachine(IAsyncStateMachine stateMachine)
            => builder.SetStateMachine(stateMachine);

        internal ValueTask<TResponse> ExecuteAsync(ProtocolStream protocol, Memory<byte> buffer, CancellationToken token)
        {
            this.protocol = protocol;
            this.buffer = buffer;
            this.token = token;

            builder = AsyncValueTaskMethodBuilder<TResponse>.Create();
            var stateMachine = this;
            builder.Start(ref stateMachine);
            return builder.Task;
        }
    }

    private readonly AsyncExclusiveLock accessLock;
    private readonly TimeSpan connectTimeout;
    private IConnectionContext? context;

    private protected Client(ILocalMember localMember, EndPoint endPoint, ClusterMemberId id)
        : base(localMember, endPoint, id)
    {
        accessLock = new();
        connectTimeout = TimeSpan.FromSeconds(1);
    }

    internal TimeSpan ConnectTimeout
    {
        get => connectTimeout;
        init => connectTimeout = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    private protected abstract ValueTask<IConnectionContext> ConnectAsync(CancellationToken token);

    private async Task<TResponse> RequestAsync<TResponse>(Request<TResponse> request, CancellationToken token)
    {
        ThrowIfDisposed();

        var timeStamp = new Timestamp();
        var lockTaken = false;

        var requestDurationTracker = CancellationTokenSource.CreateLinkedTokenSource(token);
        try
        {
            requestDurationTracker.CancelAfter(RequestTimeout);
            await accessLock.AcquireAsync(requestDurationTracker.Token).ConfigureAwait(false);
            lockTaken = true;

            context ??= await ConnectAsync(requestDurationTracker.Token).ConfigureAwait(false);

            context.Protocol.Reset();
            var result = await request.ExecuteAsync(context.Protocol, context.Buffer, requestDurationTracker.Token).ConfigureAwait(false);
            Touch();
            return result;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // canceled by caller
            context?.Dispose();
            context = null;
            throw;
        }
        catch (Exception e)
        {
            Logger.MemberUnavailable(EndPoint, e);
            Status = ClusterMemberStatus.Unavailable;

            // detect broken socket
            context?.Dispose();
            context = null;
            throw new MemberUnavailableException(this, ExceptionMessages.UnavailableMember, e);
        }
        finally
        {
            if (lockTaken)
                accessLock.Release();

            Metrics?.ReportResponseTime(timeStamp.Elapsed);
            requestDurationTracker.Dispose();
        }
    }

    public sealed override async ValueTask CancelPendingRequestsAsync()
    {
        await accessLock.StealAsync().ConfigureAwait(false);
        try
        {
            await (context?.DisposeAsync() ?? ValueTask.CompletedTask).ConfigureAwait(false);
        }
        finally
        {
            context = null;
            accessLock.Release();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            context?.Dispose();
            context = null;
            accessLock.Dispose();
        }

        base.Dispose(disposing);
    }
}