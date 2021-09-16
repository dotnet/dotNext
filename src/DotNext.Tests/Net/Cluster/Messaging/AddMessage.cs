using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Messaging;

using IO;
using Runtime.Serialization;

[ExcludeFromCodeCoverage]
public sealed class AddMessage : ISerializable<AddMessage>
{
    internal const string Name = "Add";

    public int X { get; set; }
    public int Y { get; set; }

    public int Execute() => X + Y;

    long? IDataTransferObject.Length => sizeof(int) + sizeof(int);

    async ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
    {
        await writer.WriteInt32Async(X, true, token);
        await writer.WriteInt32Async(Y, true, token);
    }

    public static async ValueTask<AddMessage> ReadFromAsync<TReader>(TReader reader, CancellationToken token)
        where TReader : notnull, IAsyncBinaryReader
        => new AddMessage
        {
            X = await reader.ReadInt32Async(true, token),
            Y = await reader.ReadInt32Async(true, token),
        };
}