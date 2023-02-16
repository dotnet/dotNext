using System.Runtime.InteropServices;

namespace DotNext.IO;

using IReadOnlySpanConsumer = Buffers.IReadOnlySpanConsumer<char>;

/// <summary>
/// Represents implementation of <see cref="IReadOnlySpanConsumer"/>
/// in the form of the writer to <see cref="TextWriter"/>.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct TextConsumer : IReadOnlySpanConsumer, IFlushable, IEquatable<TextConsumer>
{
    private readonly TextWriter output;

    /// <summary>
    /// Wraps the text writer.
    /// </summary>
    /// <param name="output">The text writer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public TextConsumer(TextWriter output)
        => this.output = output ?? throw new ArgumentNullException(nameof(output));

    /// <summary>
    /// Gets a value indicating that the underlying text writer is <see langword="null"/>.
    /// </summary>
    public bool IsEmpty => output is null;

    /// <inheritdoc />
    void IReadOnlySpanConsumer.Invoke(ReadOnlySpan<char> input) => output.Write(input);

    /// <inheritdoc />
    ValueTask ISupplier<ReadOnlyMemory<char>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<char> input, CancellationToken token)
        => new(output.WriteAsync(input, token));

    /// <inheritdoc />
    void IFlushable.Flush() => output.Flush();

    /// <inheritdoc />
    Task IFlushable.FlushAsync(CancellationToken token)
        => token.IsCancellationRequested ? Task.FromCanceled(token) : output.FlushAsync();

    /// <summary>
    /// Returns a string that represents the underlying writer.
    /// </summary>
    /// <returns>A string that represents the underlying writer.</returns>
    public override string? ToString() => output?.ToString();

    /// <summary>
    /// Wraps the text writer.
    /// </summary>
    /// <param name="output">The text writer.</param>
    /// <returns>The wrapped text writer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public static implicit operator TextConsumer(TextWriter output) => new(output);
}