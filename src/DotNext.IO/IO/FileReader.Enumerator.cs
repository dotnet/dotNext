namespace DotNext.IO;

public partial class FileReader : IAsyncEnumerable<ReadOnlyMemory<byte>>
{
    /// <inheritdoc />
    async IAsyncEnumerator<ReadOnlyMemory<byte>> IAsyncEnumerable<ReadOnlyMemory<byte>>.GetAsyncEnumerator(CancellationToken token)
    {
        for (ReadOnlyMemory<byte> buffer; length > 0L && (HasBufferedData || await ReadAsync(token).ConfigureAwait(false)); Consume(buffer.Length), length -= buffer.Length)
        {
            buffer = TrimLength(Buffer, length);
            yield return buffer;
        }
    }
}