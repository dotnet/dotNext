using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.DistributedServices
{
    using IO;
    using Messaging;
    using Text;

    internal sealed class ForcedUnlockRequest : LockMessage, IMessage, IDataTransferObject.IDecoder<ForcedUnlockRequest>
    {
        internal const string Name = "UnsafeReleaseDistributedLockRequest";

        string IMessage.Name => Name;

        bool IDataTransferObject.IsReusable => true;

        long? IDataTransferObject.Length => Encoding.GetByteCount(LockName) + sizeof(int);

        async ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        {
            var context = new EncodingContext(Encoding, true);
            await writer.WriteAsync(LockName.AsMemory(), context, StringLengthEncoding.Plain, token).ConfigureAwait(false);
        }

        async ValueTask<ForcedUnlockRequest> IDataTransferObject.IDecoder<ForcedUnlockRequest>.ReadAsync<TReader>(TReader reader, CancellationToken token)
        {
            var context = new DecodingContext(Encoding, true);
            LockName = await reader.ReadStringAsync(StringLengthEncoding.Plain, context, token).ConfigureAwait(false);
            return this;
        }
    }
}
