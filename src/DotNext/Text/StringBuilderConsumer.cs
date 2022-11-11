using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using RuntimeHelpers = System.Runtime.CompilerServices.RuntimeHelpers;

namespace DotNext.Text;

using IReadOnlySpanConsumer = Buffers.IReadOnlySpanConsumer<char>;

/// <summary>
/// Represents implementation of <see cref="IReadOnlySpanConsumer"/>
/// in the form of the writer to <see cref="StringBuilder"/>.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct StringBuilderConsumer : IReadOnlySpanConsumer, IEquatable<StringBuilderConsumer>
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
    void IReadOnlySpanConsumer.Invoke(scoped ReadOnlySpan<char> chars)
        => builder.Append(chars);

    /// <inheritdoc />
    ValueTask ISupplier<ReadOnlyMemory<char>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<char> input, CancellationToken token)
    {
        builder.Append(input.Span);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Determines whether this object contains the same builder instance as the specified object.
    /// </summary>
    /// <param name="other">The object to compare.</param>
    /// <returns><see langword="true"/> if this object contains the same builder instance as <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
    public bool Equals(StringBuilderConsumer other) => ReferenceEquals(builder, other.builder);

    /// <summary>
    /// Determines whether this object contains the same builder instance as the specified object.
    /// </summary>
    /// <param name="other">The object to compare.</param>
    /// <returns><see langword="true"/> if this object contains the same builder instance as <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
    public override bool Equals([NotNullWhen(true)] object? other) => other is StringBuilderConsumer consumer && Equals(consumer);

    /// <inheritdoc/>
    public override string? ToString() => builder?.ToString();

    /// <inheritdoc/>
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(builder);

    /// <summary>
    /// Wraps the builder.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The wrapped stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static implicit operator StringBuilderConsumer(StringBuilder builder) => new(builder);

    /// <summary>
    /// Determines whether the two objects contain references to the same builder instance.
    /// </summary>
    /// <param name="x">The first object to compare.</param>
    /// <param name="y">The second object to compare.</param>
    /// <returns><see langword="true"/> if the both objects contain references the same builder instance; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(StringBuilderConsumer x, StringBuilderConsumer y)
        => x.Equals(y);

    /// <summary>
    /// Determines whether the two objects contain references to the different builder instances.
    /// </summary>
    /// <param name="x">The first object to compare.</param>
    /// <param name="y">The second object to compare.</param>
    /// <returns><see langword="true"/> if the both objects contain references the different builder instances; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(StringBuilderConsumer x, StringBuilderConsumer y)
        => !x.Equals(y);
}