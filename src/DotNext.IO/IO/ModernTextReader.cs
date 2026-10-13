namespace DotNext.IO;

/// <summary>
/// Represents a modern base class for custom text readers that requires only necessary
/// abstract methods to work correctly.
/// </summary>
public abstract class ModernTextReader : TextReader
{
    /// <inheritdoc/>
    public sealed override Task<string?> ReadLineAsync()
        => ReadLineAsync(CancellationToken.None).AsTask();

    /// <inheritdoc/>
    public sealed override Task<int> ReadAsync(char[] buffer, int index, int count)
        => ReadAsync(new Memory<char>(buffer, index, count), CancellationToken.None).AsTask();

    /// <inheritdoc/>
    public abstract override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken token = default);
    
    /// <inheritdoc/>
    public abstract override int Read(Span<char> buffer);

    /// <inheritdoc/>
    public sealed override int Read(char[] buffer, int index, int count)
        => Read(new Span<char>(buffer, index, count));

    /// <inheritdoc/>
    public sealed override int ReadBlock(char[] buffer, int index, int count)
        => ReadBlock(new Span<char>(buffer, index, count));

    /// <inheritdoc/>
    public override int ReadBlock(Span<char> buffer)
    {
        int count, total = 0;
        do
        {
            count = Read(buffer.Slice(total));
            total += count;
        }
        while (count > 0);

        return total;
    }

    /// <inheritdoc/>
    public sealed override Task<int> ReadBlockAsync(char[] buffer, int index, int count)
        => ReadBlockAsync(new Memory<char>(buffer, index, count), CancellationToken.None).AsTask();

    /// <inheritdoc/>
    public override async ValueTask<int> ReadBlockAsync(Memory<char> buffer, CancellationToken token = default)
    {
        int count, total = 0;
        do
        {
            count = await ReadAsync(buffer.Slice(total), token).ConfigureAwait(false);
            total += count;
        } while (count > 0);

        return total;
    }

    /// <inheritdoc/>
    public sealed override Task<string> ReadToEndAsync() => ReadToEndAsync(CancellationToken.None);
}