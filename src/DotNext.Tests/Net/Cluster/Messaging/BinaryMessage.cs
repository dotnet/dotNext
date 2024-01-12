using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;

namespace DotNext.Net.Cluster.Messaging;

using IO;

[ExcludeFromCodeCoverage]
internal sealed class BinaryMessage(ReadOnlyMemory<byte> content, string name) : BinaryTransferObject(content), IMessage
{
    string IMessage.Name => name;

    ContentType IMessage.Type { get; } = new(MediaTypeNames.Application.Octet);
}