using System.IO.Pipelines;

namespace DotNext.Runtime.Serialization;

using IO;

public sealed class SerializableTests : Test
{
    [Fact]
    public static async Task SerializeDeserializeUsingPipe()
    {
        var pipe = new Pipe();
        var expected = new SerializableObject { X = 42L, Y = 56L };
        
        await expected.WriteToAsync(pipe.Writer, TestToken);
        await pipe.Writer.CompleteAsync();

        var actual = await SerializableObject.ReadFromAsync(pipe.Reader, TestToken);
        await pipe.Reader.CompleteAsync();
        Equal(expected, actual);
    }

    [Fact]
    public static async Task SerializeDeserializeUsingStream()
    {
        using var ms = new MemoryStream();
        var expected = new SerializableObject { X = 42L, Y = 56L };

        await expected.WriteToAsync(ms, token: TestToken);

        ms.Position = 0L;
        var actual = await SerializableObject.ReadFromAsync(ms, token: TestToken);
        Equal(expected, actual);
    }
    
    private sealed record SerializableObject : ISerializable<SerializableObject>
    {
        public long X, Y;
        
        public long? Length => sizeof(long) + sizeof(long);
        
        async ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        {
            await writer.WriteLittleEndianAsync(X, token);
            await writer.WriteLittleEndianAsync(Y, token);
        }

        static async ValueTask<SerializableObject> ISerializable<SerializableObject>.ReadFromAsync<TReader>(TReader reader, CancellationToken token)
            => new() { X = await reader.ReadLittleEndianAsync<long>(token), Y = await reader.ReadLittleEndianAsync<long>(token) };
    }
}