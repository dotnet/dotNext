using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext
{
    /// <summary>
    /// Various extension methods for <see cref="IDataTransferObject"/>.
    /// </summary>
    public static class DataTransferObject
    {
        /// <summary>
        /// Converts DTO content into string.
        /// </summary>
        /// <param name="content">The content to read.</param>
        /// <param name="encoding">The encoding used to decode stored string.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The content of the object.</returns>
        public static async Task<string> ReadAsTextAsync(this IDataTransferObject content, Encoding encoding, CancellationToken token = default)
        {
            using var ms = new MemoryStream(1024);
            await content.CopyToAsync(ms, token).ConfigureAwait(false);
            ms.Seek(0, SeekOrigin.Begin);
            if (ms.Length == 0L)
                return string.Empty;
            if (!ms.TryGetBuffer(out var buffer))
                buffer = new ArraySegment<byte>(ms.ToArray());
            return encoding.GetString(buffer.AsSpan());
        }
    }
}
