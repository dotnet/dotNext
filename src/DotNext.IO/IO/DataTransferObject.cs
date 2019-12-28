using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    /// <summary>
    /// Various extension methods for <see cref="IDataTransferObject"/>.
    /// </summary>
    public static class DataTransferObject
    {
        private static string ReadAsString(this MemoryStream content, Encoding encoding)
        {
            if (content.Length == 0L)
                return string.Empty;
            if (!content.TryGetBuffer(out var buffer))
                buffer = new ArraySegment<byte>(content.ToArray());
            return encoding.GetString(buffer.AsSpan());
        }

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
            return ms.ReadAsString(encoding);
        }

        /// <summary>
        /// Converts DTO content into string.
        /// </summary>
        /// <param name="content">The content to read.</param>
        /// <param name="encoding">The encoding used to decode stored string.</param>
        /// <param name="capacity">The maximum possible size of the message, in bytes.</param>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The content of the object.</returns>
        public static async Task<string> ReadAsTextAsync(this IDataTransferObject content, Encoding encoding, int capacity, CancellationToken token = default)
        {
            using var ms = new RentedMemoryStream(capacity);
            await content.CopyToAsync(ms, token).ConfigureAwait(false);
            ms.Seek(0, SeekOrigin.Begin);
            return ms.ReadAsString(encoding);
        }
    }
}
