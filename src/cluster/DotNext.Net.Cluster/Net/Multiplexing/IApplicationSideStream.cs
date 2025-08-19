using DotNext.Threading;

namespace DotNext.Net.Multiplexing;

internal interface IApplicationSideStream
{
    bool TryCompleteInput();
    bool TryCompleteOutput();
    void SendTransportSignal();
    void Consume(long count);
}