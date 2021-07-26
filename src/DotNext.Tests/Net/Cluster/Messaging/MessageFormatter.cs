using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    using IO;
    using Runtime.Serialization;

    internal sealed class MessageFormatter : IFormatter<AddMessage>, IFormatter<ResultMessage>, IFormatter<SubtractMessage>
    {
        public MessageFormatter()
        {
        }

        async ValueTask IFormatter<AddMessage>.SerializeAsync<TWriter>(AddMessage obj, TWriter writer, CancellationToken token)
        {
            await writer.WriteInt32Async(obj.X, true, token);
            await writer.WriteInt32Async(obj.Y, true, token);
        }

        async ValueTask<AddMessage> IFormatter<AddMessage>.DeserializeAsync<TReader>(TReader reader, CancellationToken token)
        {
            return new AddMessage
            {
                X = await reader.ReadInt32Async(true, token),
                Y = await reader.ReadInt32Async(true, token),
            };
        }

        long? IFormatter<AddMessage>.GetLength(AddMessage obj) => AddMessage.Size;

        async ValueTask IFormatter<SubtractMessage>.SerializeAsync<TWriter>(SubtractMessage obj, TWriter writer, CancellationToken token)
        {
            await writer.WriteInt32Async(obj.X, true, token);
            await writer.WriteInt32Async(obj.Y, true, token);
        }

        async ValueTask<SubtractMessage> IFormatter<SubtractMessage>.DeserializeAsync<TReader>(TReader reader, CancellationToken token)
        {
            return new SubtractMessage
            {
                X = await reader.ReadInt32Async(true, token),
                Y = await reader.ReadInt32Async(true, token),
            };
        }

        long? IFormatter<SubtractMessage>.GetLength(SubtractMessage obj) => SubtractMessage.Size;

        async ValueTask IFormatter<ResultMessage>.SerializeAsync<TWriter>(ResultMessage obj, TWriter writer, CancellationToken token)
        {
            await writer.WriteInt32Async(obj.Result, true, token);
        }

        async ValueTask<ResultMessage> IFormatter<ResultMessage>.DeserializeAsync<TReader>(TReader reader, CancellationToken token)
        {
            return new ResultMessage
            {
                Result = await reader.ReadInt32Async(true, token),
            };
        }

        long? IFormatter<ResultMessage>.GetLength(ResultMessage obj) => ResultMessage.Size;
    }
}