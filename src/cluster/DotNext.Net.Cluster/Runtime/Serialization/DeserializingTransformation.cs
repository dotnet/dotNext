using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Runtime.Serialization
{
    using IO;

    [StructLayout(LayoutKind.Auto)]
    internal readonly struct DeserializingTransformation<T> : IDataTransferObject.ITransformation<T>
    {
        private readonly IFormatter<T> formatter;

        public DeserializingTransformation(IFormatter<T> formatter)
        {
            this.formatter = formatter;
        }

        ValueTask<T> IDataTransferObject.ITransformation<T>.TransformAsync<TReader>(TReader reader, CancellationToken token)
            => formatter.DeserializeAsync(reader, token);
    }
}