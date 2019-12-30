using System;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Net.Cluster.DistributedServices
{
    using IO;
    using Messaging;
    using Text;
    using DistributedLockInfo = Threading.DistributedLockInfo;

    internal sealed class AcquireLockRequest : IMessage, IDataTransferObject.IDecoder<AcquireLockRequest>
    {
        private const int BufferSize = 512;
        internal const string Name = "AcquireDistributedLockRequest";
        private string? lockName;
        internal DistributedLockInfo LockInfo;

        internal string LockName
        {
            get => lockName ?? string.Empty;
            set => lockName = value;
        }

        private static Encoding Encoding => Encoding.Unicode;

        string IMessage.Name => Name;

        bool IDataTransferObject.IsReusable => true;

        long? IDataTransferObject.Length => Unsafe.SizeOf<DistributedLockInfo>() + Encoding.GetByteCount(LockName);
    
        ContentType IMessage.Type => new ContentType(MediaTypeNames.Application.Octet);

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
            LockInfo = await reader.ReadAsync<DistributedLockInfo>(token).ConfigureAwait(false);
            return this;
        }
    }

    internal sealed class AcquireLockResponse : BinaryMessage<bool>
    {
        internal static readonly MessageReader<bool> Reader = DataTransferObject.ToType<bool, IMessage>;
        internal new const string Name = "AcquireDistributedLockResponse";

        internal AcquireLockResponse()
            : base(Name, null)
        {
        }
    }
}