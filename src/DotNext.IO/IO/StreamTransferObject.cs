using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    /// <summary>
    /// Represents object which content is represented by <see cref="Stream"/>.
    /// </summary>
    public class StreamTransferObject : Disposable, IDataTransferObject, IAsyncDisposable
    {
        private const int DefaultBufferSize = 1024;
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


        async ValueTask IDataTransferObject.CopyToAsync(Stream output, CancellationToken token)
        {
            await content.CopyToAsync(output, DefaultBufferSize, token).ConfigureAwait(false);
            if (content.CanSeek)
                content.Seek(0, SeekOrigin.Begin);
        }

        async ValueTask IDataTransferObject.CopyToAsync(PipeWriter output, CancellationToken token)
        {
            await content.ReadAsync(output, DefaultBufferSize, token).ConfigureAwait(false);
            if (content.CanSeek)
                content.Seek(0, SeekOrigin.Begin);
        }

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

        /// <summary>
        /// Asynchronously releases the resources associated with this object.
        /// </summary>
        /// <returns>A task that represents the asynchronous dispose operation.</returns>
        public virtual ValueTask DisposeAsync()
        {
            base.Dispose(false);
            return content.DisposeAsync();
        }
    }
}
