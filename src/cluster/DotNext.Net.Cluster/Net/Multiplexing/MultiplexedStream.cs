using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Connections;

namespace DotNext.Net.Multiplexing;

using Threading;

internal sealed partial class MultiplexedStream : IDuplexPipe, IApplicationSideStream
{
    private const int MaxFrameSize = ushort.MaxValue;
    
    private const uint AppInputCompletedState = 0B_0001;
    private const uint TransportInputCompletedState = 0B_0010;
    private const uint AppOutputCompletedState = 0B_0100;
    private const uint TransportOutputCompletedState = 0B_1000;

    private const uint AppSideCompletedState = AppInputCompletedState | AppOutputCompletedState;
    private const uint TransportSideCompletedState = TransportInputCompletedState | TransportOutputCompletedState;
    private const uint CompletedState = AppSideCompletedState | TransportSideCompletedState;

    private readonly PipeReader transportReader;
    private readonly PipeWriter transportWriter;
    private readonly AppSideWriter appWriter;
    private readonly AppSideReader appReader;
    private readonly AsyncAutoResetEvent transportSignal;

    private readonly int frameSize;
    private volatile uint state;

    public MultiplexedStream(PipeOptions options, AsyncAutoResetEvent signal)
    {
        transportSignal = signal;

        var input = new Pipe(options);
        transportReader = input.Reader;
        appWriter = new(this, input.Writer);

        var output = new Pipe(options);
        transportWriter = output.Writer;
        appReader = new(this, output.Reader);

        resumeThreshold = options.ResumeWriterThreshold;
        inputWindow = int.CreateSaturating(options.PauseWriterThreshold);
        frameSize = GetFrameSize(options);
    }

    internal static int GetFrameSize(PipeOptions options)
        => (int)long.Min(MaxFrameSize, options.ResumeWriterThreshold);

    AsyncAutoResetEvent IApplicationSideStream.TransportSignal => transportSignal;
    
    public ValueTask CompleteTransportInputAsync(Exception? e = null)
        => TryCompleteTransportInput() ? transportReader.CompleteAsync(e) : ValueTask.CompletedTask;

    public ValueTask CompleteTransportOutputAsync(Exception? e = null)
        => TryCompleteTransportOutput() ? transportWriter.CompleteAsync(e) : ValueTask.CompletedTask;

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

    public bool IsCompleted => (state & CompletedState) is CompletedState;

    private bool IsTransportInputCompleted(out bool appSideCompleted)
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