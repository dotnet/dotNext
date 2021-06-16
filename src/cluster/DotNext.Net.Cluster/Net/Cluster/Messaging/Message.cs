using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    using IO;
    using Runtime.Serialization;

    internal sealed class Message<T> : IMessage
    {
        private readonly IFormatter<T> formatter;
        private readonly T payload;

        internal Message(string name, T payload, IFormatter<T> formatter, string? type = null)
        {
            Name = name;
            Type = new ContentType(type ?? MediaTypeNames.Application.Octet);
            this.formatter = formatter;
            this.payload = payload;
        }

        public string Name { get; }

        public ContentType Type { get; }

        long? IDataTransferObject.Length => formatter.GetLength(payload);

        bool IDataTransferObject.IsReusable => true;

        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            => formatter.SerializeAsync(payload, writer, token);
    }
}