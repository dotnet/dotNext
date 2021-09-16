using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Messaging;

using IO;
using Runtime.Serialization;

[ExcludeFromCodeCoverage]
public sealed class SubtractMessage : ISerializable<SubtractMessage>
{
    internal const string Name = "Subtract";

    public int X { get; set; }
    public int Y { get; set; }

    public int Execute() => X - Y;

    long? IDataTransferObject.Length => sizeof(int) + sizeof(int);

    async ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
    {
        await writer.WriteInt32Async(X, true, token);
        await writer.WriteInt32Async(Y, true, token);
    }

    public static async ValueTask<SubtractMessage> ReadFromAsync<TReader>(TReader reader, CancellationToken token)
        where TReader : notnull, IAsyncBinaryReader
        => new SubtractMessage
        {
            X = await reader.ReadInt32Async(true, token),
            Y = await reader.ReadInt32Async(true, token),
        };
}