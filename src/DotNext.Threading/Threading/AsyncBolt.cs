using System.Threading;

namespace DotNext.Threading
{
    public class AsyncBolt : Synchronizer, IAsyncResetEvent
    {
        EventResetMode IAsyncResetEvent.ResetMode => EventResetMode.AutoReset;
    }
}