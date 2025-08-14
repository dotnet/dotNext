using DotNext.Threading;

namespace DotNext.Net.Multiplexing;

internal interface IApplicationSideStream
{
    bool TryCompleteInput();
    bool TryCompleteOutput();
    
    AsyncAutoResetEvent TransportSignal { get; }

    void Consume(long count);
}