using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Multiplexing;

internal abstract class StreamHandlerBase
{
    private const uint AppInputCompletedState = 0B_0001;
    private const uint TransportInputCompletedState = 0B_0010;
    private const uint AppOutputCompletedState = 0B_0100;
    private const uint TransportOutputCompletedState = 0B_1000;

    private const uint AppSideCompletedState = AppInputCompletedState | AppOutputCompletedState;
    private const uint TransportSideCompletedState = TransportInputCompletedState | TransportOutputCompletedState;

    private volatile uint state;

    private bool TryComplete([ConstantExpected] uint flag)
    {
        var stateCopy = Interlocked.Or(ref state, flag);
        return (stateCopy & flag) is 0U;
    }

    public bool TryCompleteAppInput() => TryComplete(AppInputCompletedState);

    public bool TryCompleteAppOutput() => TryComplete(AppOutputCompletedState);

    protected bool TryCompleteTransportInput() => TryComplete(TransportInputCompletedState);

    protected bool TryCompleteTransportOutput() => TryComplete(TransportOutputCompletedState);

    public bool IsTransportSideCompleted => (state & TransportSideCompletedState) is TransportSideCompletedState;

    public bool IsTransportOutputCompleted => (state & TransportOutputCompletedState) is not 0U;

    public bool IsTransportInputCompleted(out bool appSideCompleted)
    {
        var stateCopy = state;
        appSideCompleted = (stateCopy & AppSideCompletedState) is AppSideCompletedState;
        return (stateCopy & TransportInputCompletedState) is not 0U;
    }
}