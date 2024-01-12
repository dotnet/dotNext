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
        await writer.WriteLittleEndianAsync(X, token);
        await writer.WriteLittleEndianAsync(Y, token);
    }

    public static async ValueTask<AddMessage> ReadFromAsync<TReader>(TReader reader, CancellationToken token)
        where TReader : notnull, IAsyncBinaryReader
        => new AddMessage
        {
            X = await reader.ReadLittleEndianAsync<int>(token),
            Y = await reader.ReadLittleEndianAsync<int>(token),
        };
}