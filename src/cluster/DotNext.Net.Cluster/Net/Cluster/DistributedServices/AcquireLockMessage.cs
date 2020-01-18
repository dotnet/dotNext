using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Net.Cluster.DistributedServices
{
    using IO;
    using Messaging;
    using Text;

    internal sealed class AcquireLockRequest : LockMessage, IMessage, IDataTransferObject.IDecoder<AcquireLockRequest>
    {
        internal const string Name = "AcquireDistributedLockRequest";
        internal DistributedLock LockInfo;

        string IMessage.Name => Name;

        bool IDataTransferObject.IsReusable => true;

        long? IDataTransferObject.Length => Unsafe.SizeOf<DistributedLock>() + Encoding.GetByteCount(LockName) + sizeof(int);

        async ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        {
            var context = new EncodingContext(Encoding, true);
            await writer.WriteAsync(LockName.AsMemory(), context, StringLengthEncoding.Plain, token).ConfigureAwait(false);
            await writer.WriteAsync(LockInfo, token).ConfigureAwait(false);
        }

        async ValueTask<AcquireLockRequest> IDataTransferObject.IDecoder<AcquireLockRequest>.ReadAsync<TReader>(TReader reader, CancellationToken token)
        {
            var context = new DecodingContext(Encoding, true);
            LockName = await reader.ReadStringAsync(StringLengthEncoding.Plain, context, token).ConfigureAwait(false);
            LockInfo = await reader.ReadAsync<DistributedLock>(token).ConfigureAwait(false);
            return this;
        }
    }

    internal sealed class AcquireLockResponse : BinaryMessage<bool>
    {
        private new const string Name = "AcquireDistributedLockResponse";

        internal AcquireLockResponse()
            : base(Name, null)
        {
        }
    }
}