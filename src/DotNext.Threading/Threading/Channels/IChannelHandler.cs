namespace DotNext.Threading.Channels
{
    internal interface IChannelHandler
    {
        long Position { get; }
    }
}
