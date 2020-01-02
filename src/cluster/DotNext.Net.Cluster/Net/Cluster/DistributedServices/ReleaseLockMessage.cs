using System;
using System.Threading;
using System.Threading.Tasks;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Net.Cluster.DistributedServices
{
    using IO;
    using Messaging;
    using Text;

    internal sealed class ReleaseLockRequest : LockMessage, IMessage, IDataTransferObject.IDecoder<ReleaseLockRequest>
    {
        internal const string Name = "ReleaseDistributedLockRequest";
        
        internal Guid Owner, Version;

        string IMessage.Name => Name;

        bool IDataTransferObject.IsReusable => true;

        long? IDataTransferObject.Length 
            => Unsafe.SizeOf<Guid>() * 2  + Encoding.GetByteCount(LockName);
    
        async ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        {
            var context = new EncodingContext(Encoding, true);
            await writer.WriteAsync(LockName.AsMemory(), context, StringLengthEncoding.Plain, token).ConfigureAwait(false);
            await writer.WriteAsync(Owner, token).ConfigureAwait(false);
            await writer.WriteAsync(Version, token).ConfigureAwait(false);
        }

        async ValueTask<ReleaseLockRequest> IDataTransferObject.IDecoder<ReleaseLockRequest>.ReadAsync<TReader>(TReader reader, CancellationToken token)
        {
            var context = new DecodingContext(Encoding, true);
            LockName = await reader.ReadStringAsync(StringLengthEncoding.Plain, context, token).ConfigureAwait(false);
            Owner = await reader.ReadAsync<Guid>(token).ConfigureAwait(false);
            Version = await reader.ReadAsync<Guid>(token).ConfigureAwait(false);
            return this;
        }
    }

    internal sealed class ReleaseLockResponse : BinaryMessage<bool>
    {
        private new const string Name = "ReleaseDistributedLockResponse";

        internal ReleaseLockResponse()
            : base(Name, null)
        {
        }
    }
}