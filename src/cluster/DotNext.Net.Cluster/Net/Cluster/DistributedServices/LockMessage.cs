using System.Net.Mime;
using System.Text;

namespace DotNext.Net.Cluster.DistributedServices
{
    internal abstract class LockMessage
    {
        private string? lockName;

        internal string LockName
        {
            get => lockName ?? string.Empty;
            set => lockName = value;
        }

        public ContentType Type { get; } = new ContentType(MediaTypeNames.Application.Octet);

        private protected static Encoding Encoding => Encoding.Unicode;
    }
}