using System.IO;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    public sealed class TextMessage : IMessage<string>
    {
        public TextMessage(string name, string value)
            : this(name, value, null)
        {
            
        }

        internal TextMessage(string name, string value, string mediaType)
        {
            Content = value;
            Type = new ContentType() { MediaType = mediaType.IfNullOrEmpty(MediaTypeNames.Text.Plain), CharSet = "utf-8" };
            Name = name;
        }

        /// <summary>
        /// Gets name of this message.
        /// </summary>
        public string Name { get; }

        private Encoding Encoding => string.IsNullOrEmpty(Type.CharSet) ? Encoding.UTF8 : Encoding.GetEncoding(Type.CharSet);

        long? IMessage.Length => Encoding.GetByteCount(Content);

        public string Content { get; }

        async Task IMessage.CopyToAsync(Stream output)
        {
            using(var writer = new StreamWriter(output, Encoding, 256, true))
                await writer.WriteAsync(Content);  
        }

        /// <summary>
        /// MIME type of the message.
        /// </summary>
        public ContentType Type { get; } 
    }
}