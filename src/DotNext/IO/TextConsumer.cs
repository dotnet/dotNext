using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using RuntimeHelpers = System.Runtime.CompilerServices.RuntimeHelpers;

namespace DotNext.IO;

using IReadOnlySpanConsumer = Buffers.IReadOnlySpanConsumer<char>;

/// <summary>
/// Represents implementation of <see cref="IReadOnlySpanConsumer"/>
/// in the form of the writer to <see cref="TextWriter"/>.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct TextConsumer : IReadOnlySpanConsumer, IFlushable
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
    void IReadOnlySpanConsumer.Invoke(scoped ReadOnlySpan<char> input) => output.Write(input);

    /// <inheritdoc />
    ValueTask ISupplier<ReadOnlyMemory<char>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<char> input, CancellationToken token)
        => new(output.WriteAsync(input, token));

    /// <inheritdoc />
    void IFlushable.Flush() => output.Flush();

    /// <inheritdoc />
    Task IFlushable.FlushAsync(CancellationToken token)
        => token.IsCancellationRequested ? Task.FromCanceled(token) : output.FlushAsync();

    /// <summary>
    /// Determines whether this object contains the same text writer as the specified object.
    /// </summary>
    /// <param name="other">The object to compare.</param>
    /// <returns><see langword="true"/> if this object contains the same text writer as <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
    public bool Equals(TextConsumer other) => ReferenceEquals(output, other.output);

    /// <summary>
    /// Determines whether this object contains the same text writer as the specified object.
    /// </summary>
    /// <param name="other">The object to compare.</param>
    /// <returns><see langword="true"/> if this object contains the same text writer as <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
    public override bool Equals([NotNullWhen(true)] object? other) => other is TextConsumer consumer && Equals(consumer);

    /// <summary>
    /// Gets the hash code representing identity of the stored text writer.
    /// </summary>
    /// <returns>The hash code representing identity of the stored text writer.</returns>
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(output);

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

    /// <summary>
    /// Determines whether the two objects contain references to the same text writer.
    /// </summary>
    /// <param name="x">The first object to compare.</param>
    /// <param name="y">The second object to compare.</param>
    /// <returns><see langword="true"/> if the both objects contain references the same text writer; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(TextConsumer x, TextConsumer y)
        => x.Equals(y);

    /// <summary>
    /// Determines whether the two objects contain references to the different text writers.
    /// </summary>
    /// <param name="x">The first object to compare.</param>
    /// <param name="y">The second object to compare.</param>
    /// <returns><see langword="true"/> if the both objects contain references the different text writers; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(TextConsumer x, TextConsumer y)
        => !x.Equals(y);
}