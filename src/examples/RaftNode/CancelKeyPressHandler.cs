using DotNext.Threading;

namespace RaftNode;

internal sealed class CancelKeyPressHandler : AsyncManualResetEvent
{
    internal CancelKeyPressHandler()
        : base(false)
    {
    }

    internal void Handler(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        Set();
    }
}