using System.Runtime.InteropServices;
using System.Text;

namespace DotNext.Text;

using IReadOnlySpanConsumer = Buffers.IReadOnlySpanConsumer<char>;

/// <summary>
/// Represents implementation of <see cref="IReadOnlySpanConsumer"/>
/// in the form of the writer to <see cref="StringBuilder"/>.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct StringBuilderConsumer : IReadOnlySpanConsumer, IEquatable<StringBuilderConsumer>
{
    private readonly StringBuilder builder;

    /// <summary>
    /// Wraps the builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public StringBuilderConsumer(StringBuilder builder)
        => this.builder = builder ?? throw new ArgumentNullException(nameof(builder));

    /// <summary>
    /// Gets a value indicating that the underlying builder is <see langword="null"/>.
    /// </summary>
    public bool IsEmpty => builder is null;

    /// <inheritdoc />
    void IReadOnlySpanConsumer.Invoke(ReadOnlySpan<char> chars)
        => builder.Append(chars);

    /// <inheritdoc />
    ValueTask ISupplier<ReadOnlyMemory<char>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<char> input, CancellationToken token)
    {
        builder.Append(input.Span);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public override string? ToString() => builder?.ToString();

    /// <summary>
    /// Wraps the builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The wrapped stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static implicit operator StringBuilderConsumer(StringBuilder builder) => new(builder);
}