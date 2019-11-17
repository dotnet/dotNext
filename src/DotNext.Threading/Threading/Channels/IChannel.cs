using System.IO;

namespace DotNext.Threading.Channels
{
    using IO;

    internal interface IChannel
    {
        DirectoryInfo Location { get; }

        TopicStream GetOrCreateTopic(ref State state, ref TopicStream topic, in FileCreationOptions options, bool deleteOnDispose);
    }
}
