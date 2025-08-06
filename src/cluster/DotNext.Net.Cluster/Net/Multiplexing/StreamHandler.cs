using System.IO.Pipelines;

namespace DotNext.Net.Multiplexing;

using Threading;

internal sealed partial class StreamHandler : StreamHandlerBase, IDuplexPipe
{
    private readonly PipeWriter appWriter;
    private readonly PipeReader appReader;

    public StreamHandler(PipeOptions options, AsyncAutoResetEvent writeSignal)
    {
        var input = new Pipe(options);
        appWriter = new AppSideWriter(this, input.Writer, writeSignal);
        Input = input.Reader;

        var output = new Pipe(options);
        appReader = new AppSideReader(this, output.Reader, writeSignal);
        Output = output.Writer;
    }

    public void CancelAppSide()
    {
        appWriter.CancelPendingFlush();
        appReader.CancelPendingRead();
    }

    public ValueTask CompleteTransportInputAsync(Exception? e = null)
        => TryCompleteTransportInput() ? Input.CompleteAsync(e) : ValueTask.CompletedTask;

    public ValueTask CompleteTransportOutputAsync(Exception? e = null)
        => TryCompleteTransportOutput() ? Output.CompleteAsync(e) : ValueTask.CompletedTask;

    // transport input, that accepts the data from the app output
    public PipeReader Input { get; }

    // transport output, that passes the data to the app input
    public PipeWriter Output { get; }

    // app input
    PipeReader IDuplexPipe.Input => appReader;

    // app output
    PipeWriter IDuplexPipe.Output => appWriter;
}