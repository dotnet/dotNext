using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext
{
    using IO;

    /// <summary>
    /// Represents object which content is represented by <see cref="Stream"/>.
    /// </summary>
    public class StreamTransferObject : Disposable, IDataTransferObject
    {
        private readonly bool leaveOpen;
        private readonly Stream content;

        /// <summary>
        /// Initializes a new message.
        /// </summary>
        /// <param name="content">The message content.</param>
        /// <param name="leaveOpen"><see langword="true"/> to leave the stream open after <see cref="StreamTransferObject"/> object is disposed; otherwise, <see langword="false"/>.</param>
        public StreamTransferObject(Stream content, bool leaveOpen)
        {
            this.leaveOpen = leaveOpen;
            this.content = content;
        }

        /// <summary>
        /// Indicates that the content of this message can be copied to the output stream or pipe multiple times.
        /// </summary>
        public virtual bool IsReusable => content.CanSeek;

        long? IDataTransferObject.Length => content.CanSeek ? content.Length : default(long?);

        private static async Task CopyToAsyncAndSeek(Stream input, Stream output, CancellationToken token)
        {
            await input.CopyToAsync(output, 1024, token).ConfigureAwait(false);
            input.Seek(0, SeekOrigin.Begin);
        }

        Task IDataTransferObject.CopyToAsync(Stream output, CancellationToken token) =>
            content.CanSeek ? CopyToAsyncAndSeek(content, output, token) : content.CopyToAsync(output, 1024, token);

        ValueTask IDataTransferObject.CopyToAsync(PipeWriter output, CancellationToken token)
            => content.CopyToAsync(output, true, token: token);

        /// <summary>
        /// Releases resources associated with this object.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> if called from <see cref="Disposable.Dispose()"/>; <see langword="false"/> if called from finalizer <see cref="Disposable.Finalize()"/>.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && !leaveOpen)
                content.Dispose();
            base.Dispose(disposing);
        }
    }
}
