namespace DotNext.IO;

internal abstract class WriterStream<TOutput> : ModernStream, IFlushable
    where TOutput : IFlushable
{
    // not readonly to avoid defensive copying
    private protected TOutput output;
    private protected long writtenBytes;

    private protected WriterStream(TOutput output) => this.output = output;

    public sealed override bool CanRead => false;

    public sealed override bool CanWrite => true;

    public sealed override bool CanSeek => false;

    public override bool CanTimeout => false;

    public sealed override long Position
    {
        get => writtenBytes;
        set => throw new NotSupportedException();
    }

    public sealed override long Length => writtenBytes;

    public sealed override void SetLength(long value) => throw new NotSupportedException();

    public sealed override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public sealed override void CopyTo(Stream destination, int bufferSize) => throw new NotSupportedException();

    public sealed override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken token)
        => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.FromException(new NotSupportedException());

    public sealed override void Flush() => output.Flush();

    public sealed override Task FlushAsync(CancellationToken token) => output.FlushAsync(token);

    public sealed override int Read(Span<byte> buffer) => throw new NotSupportedException();

    public sealed override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        => token.IsCancellationRequested ? ValueTask.FromCanceled<int>(token) : ValueTask.FromException<int>(new NotSupportedException());
}