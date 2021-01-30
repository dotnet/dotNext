using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using RuntimeHelpers = System.Runtime.CompilerServices.RuntimeHelpers;

namespace DotNext.IO
{
    using IReadOnlySpanConsumer = Buffers.IReadOnlySpanConsumer<byte>;

    /// <summary>
    /// Represents implementation of <see cref="IReadOnlySpanConsumer"/>
    /// in the form of the writer to <see cref="Stream"/>.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct StreamConsumer : IReadOnlySpanConsumer, IEquatable<StreamConsumer>, IFlushable
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
        void IFlushable.Flush() => output.Flush();

        /// <inheritdoc />
        Task IFlushable.FlushAsync(CancellationToken token) => output.FlushAsync(token);

        /// <summary>
        /// Determines whether this object contains the same stream instance as the specified object.
        /// </summary>
        /// <param name="other">The object to compare.</param>
        /// <returns><see langword="true"/> if this object contains the same stream instance as <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public bool Equals(StreamConsumer other) => ReferenceEquals(output, other.output);

        /// <summary>
        /// Determines whether this object contains the same stream instance as the specified object.
        /// </summary>
        /// <param name="other">The object to compare.</param>
        /// <returns><see langword="true"/> if this object contains the same buffer instance as <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other is StreamConsumer consumer && Equals(consumer);

        /// <summary>
        /// Gets the hash code representing identity of the stored stream instance.
        /// </summary>
        /// <returns>The hash code representing identity of the stored stream instance.</returns>
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(output);

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
        public static implicit operator StreamConsumer(Stream output) => new StreamConsumer(output);

        /// <summary>
        /// Determines whether the two objects contain references to the same stream instance.
        /// </summary>
        /// <param name="x">The first object to compare.</param>
        /// <param name="y">The second object to compare.</param>
        /// <returns><see langword="true"/> if the both objects contain references the same stream instance; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(StreamConsumer x, StreamConsumer y)
            => x.Equals(y);

        /// <summary>
        /// Determines whether the two objects contain references to the different stream instances.
        /// </summary>
        /// <param name="x">The first object to compare.</param>
        /// <param name="y">The second object to compare.</param>
        /// <returns><see langword="true"/> if the both objects contain references the different stream instances; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(StreamConsumer x, StreamConsumer y)
            => !x.Equals(y);
    }
}