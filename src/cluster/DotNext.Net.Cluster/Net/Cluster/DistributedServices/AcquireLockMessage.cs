using System;
using System.IO;
using System.IO.Pipelines;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Net.Cluster.DistributedServices
{
    using Buffers;
    using IO;
    using Messaging;
    using DistributedLockInfo = Threading.DistributedLockInfo;
    using static IO.Pipelines.PipeExtensions;

    internal sealed class AcquireLockRequest : IMessage
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

        async ValueTask IDataTransferObject.CopyToAsync(Stream output, CancellationToken token)
        {
            using var buffer = new ArrayRental<byte>(BufferSize);
            await output.WriteStringAsync(LockName.AsMemory(), Encoding, buffer.Memory, StringLengthEncoding.Plain, token).ConfigureAwait(false);
            await output.WriteAsync(LockInfo, buffer.Memory, token).ConfigureAwait(false);
        }

        async ValueTask IDataTransferObject.CopyToAsync(PipeWriter output, CancellationToken token)
        {
            await output.WriteStringAsync(LockName.AsMemory(), Encoding, lengthFormat: StringLengthEncoding.Plain, token: token);
            await output.WriteAsync(LockInfo, token).ConfigureAwait(false);
        }
    }
}