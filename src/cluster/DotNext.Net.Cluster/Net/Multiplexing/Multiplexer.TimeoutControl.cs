namespace DotNext.Net.Multiplexing;

using Threading;

partial class Multiplexer
{
    public required CancellationTokenMultiplexer TokenMultiplexer;
    public required CancellationToken RootToken;
}