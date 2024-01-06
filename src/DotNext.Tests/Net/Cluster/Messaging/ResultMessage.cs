using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Messaging;

using IO;
using Runtime.Serialization;

[ExcludeFromCodeCoverage]
public sealed class ResultMessage : ISerializable<ResultMessage>
{
    internal const string Name = "Result";

    public int Result { get; set; }

    long? IDataTransferObject.Length => sizeof(int);

    ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        => writer.WriteLittleEndianAsync(Result, token);

    public static async ValueTask<ResultMessage> ReadFromAsync<TReader>(TReader reader, CancellationToken token)
        where TReader : notnull, IAsyncBinaryReader
        => await reader.ReadLittleEndianAsync<int>(token);

    public static implicit operator ResultMessage(int value) => new() { Result = value };
}