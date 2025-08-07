using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Connections;

namespace DotNext.Net.Multiplexing;

using Threading;

internal sealed partial class MultiplexedStream : IDuplexPipe, IApplicationSideStream
{
    private const uint AppInputCompletedState = 0B_0001;
    private const uint TransportInputCompletedState = 0B_0010;
    private const uint AppOutputCompletedState = 0B_0100;
    private const uint TransportOutputCompletedState = 0B_1000;

    private const uint AppSideCompletedState = AppInputCompletedState | AppOutputCompletedState;
    private const uint TransportSideCompletedState = TransportInputCompletedState | TransportOutputCompletedState;

    /// <summary>
    /// Represents transport-side input.
    /// </summary>
    public readonly PipeReader Input;
    
    /// <summary>
    /// Represents transport-side output.
    /// </summary>
    public readonly PipeWriter Output;
    
    private readonly AppSideWriter appWriter;
    private readonly AppSideReader appReader;
    private volatile uint state;

    public MultiplexedStream(PipeOptions options, AsyncAutoResetEvent writeSignal)
    {
        var input = new Pipe(options);
        Input = input.Reader;
        appWriter = new(this, input.Writer, writeSignal);

        var output = new Pipe(options);
        Output = output.Writer;
        appReader = new(this, output.Reader, writeSignal);
    }
    
    public ValueTask CompleteTransportInputAsync(Exception? e = null)
        => TryCompleteTransportInput() ? Input.CompleteAsync(e) : ValueTask.CompletedTask;

    public ValueTask CompleteTransportOutputAsync(Exception? e = null)
        => TryCompleteTransportOutput() ? Output.CompleteAsync(e) : ValueTask.CompletedTask;

    private bool TryComplete([ConstantExpected] uint flag)
    {
        var stateCopy = Interlocked.Or(ref state, flag);
        return (stateCopy & flag) is 0U;
    }

    bool IApplicationSideStream.TryCompleteInput() => TryComplete(AppInputCompletedState);

    bool IApplicationSideStream.TryCompleteOutput() => TryComplete(AppOutputCompletedState);

    private bool TryCompleteTransportInput() => TryComplete(TransportInputCompletedState);

    private bool TryCompleteTransportOutput() => TryComplete(TransportOutputCompletedState);

    public bool IsTransportSideCompleted => (state & TransportSideCompletedState) is TransportSideCompletedState;

    public bool IsTransportOutputCompleted => (state & TransportOutputCompletedState) is not 0U;

    public bool IsTransportInputCompleted(out bool appSideCompleted)
    {
        var stateCopy = state;
        appSideCompleted = (stateCopy & AppSideCompletedState) is AppSideCompletedState;
        return (stateCopy & TransportInputCompletedState) is not 0U;
    }

    public async ValueTask AbortAppSideAsync()
    {
        var e = new ConnectionAbortedException();
        ExceptionDispatchInfo.SetCurrentStackTrace(e);

        await appWriter.CompleteAsync(e).ConfigureAwait(false);
        await appReader.CompleteAsync(e).ConfigureAwait(false);
    }

    // app input
    PipeReader IDuplexPipe.Input => appReader;

    // app output
    PipeWriter IDuplexPipe.Output => appWriter;
}