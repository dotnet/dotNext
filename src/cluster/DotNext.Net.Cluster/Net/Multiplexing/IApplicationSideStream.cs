namespace DotNext.Net.Multiplexing;

internal interface IApplicationSideStream
{
    bool TryCompleteInput();
    bool TryCompleteOutput();
}