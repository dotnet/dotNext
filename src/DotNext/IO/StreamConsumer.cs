using System.Runtime.InteropServices;

namespace DotNext.IO;

using Runtime.CompilerServices;
using IReadOnlySpanConsumer = Buffers.IReadOnlySpanConsumer<byte>;

/// <summary>
/// Represents implementation of <see cref="IReadOnlySpanConsumer"/>
/// in the form of the writer to <see cref="Stream"/>.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct StreamConsumer : IReadOnlySpanConsumer, IEquatable<StreamConsumer>, IFlushable
{
    private readonly Stream output;

    /// <summary>
    /// Wraps the stream.
    /// </summary>
    /// <param name="output">The writable stream.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public StreamConsumer(Stream output) => this.output = output ?? throw new ArgumentNullException(nameof(output));

    /// <summary>
    /// Gets a value indicating that the underlying stream is <see langword="null"/>.
    /// </summary>
    public bool IsEmpty => output is null;

    /// <inheritdoc />
    void IReadOnlySpanConsumer.Invoke(ReadOnlySpan<byte> input) => output.Write(input);

    /// <inheritdoc />
    ValueTask ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<byte> input, CancellationToken token)
        => output.WriteAsync(input, token);

    /// <inheritdoc />
    Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> IFunctional<Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask>>.ToDelegate()
        => output.WriteAsync;

    /// <inheritdoc />
    void IFlushable.Flush() => output.Flush();

    /// <inheritdoc />
    Task IFlushable.FlushAsync(CancellationToken token) => output.FlushAsync(token);

    /// <summary>
    /// Returns a string that represents the underlying stream.
    /// </summary>
    /// <returns>A string that represents the underlying stream.</returns>
    public override string? ToString() => output?.ToString();

    /// <summary>
    /// Wraps the stream.
    /// </summary>
    /// <param name="output">The writable stream.</param>
    /// <returns>The wrapped stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public static implicit operator StreamConsumer(Stream output) => new(output);
}