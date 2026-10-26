using System.Runtime.InteropServices;

namespace DotNext.Runtime.Caching;

using InteropServices;

/// <summary>
/// Provides various helpers to work with <see cref="RandomAccessCache{TValue}"/>.
/// </summary>
public static class RandomAccessCache
{
    internal static int DefaultConcurrencyLevel => Environment.ProcessorCount * 2 + 1;
    
    /// <summary>
    /// Returns <see cref="ReadOnlyMemory{T}"/> as a key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The object that can be used as a key in <see cref="RandomAccessCache{TValue}"/>.</returns>
    public static ReadOnlyMemoryKey AsKey(in ReadOnlyMemory<byte> key)
        => new(in key);

    /// <summary>
    /// Returns <see cref="string"/> as a key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The object that can be used as a key in <see cref="RandomAccessCache{TValue}"/>.</returns>
    public static StringKey AsKey(string? key) => new(key);
    
    /// <summary>
    /// Represents <see cref="ReadOnlyMemory{T}"/> of <see cref="byte"/> as a key for <see cref="RandomAccessCache{TValue}"/>.
    /// </summary>
    /// <param name="key">The key.</param>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ReadOnlyMemoryKey(in ReadOnlyMemory<byte> key) : ISupplier<ReadOnlySpan<byte>>
    {
        private readonly ReadOnlyMemory<byte> key = key;
        
        /// <inheritdoc/>
        ReadOnlySpan<byte> ISupplier<ReadOnlySpan<byte>>.Invoke()
            => key.Span;

        /// <summary>
        /// Converts <paramref name="key"/> to <see cref="ReadOnlySpan{T}"/> over <see cref="byte"/>.
        /// </summary>
        /// <param name="key">The key to convert.</param>
        /// <returns><see cref="ReadOnlySpan{T}"/> over <see cref="byte"/> which represents the key.</returns>
        public static implicit operator ReadOnlySpan<byte>(ReadOnlyMemoryKey key)
            => key.key.Span;
    }

    /// <summary>
    /// Represents <see cref="string"/> as a key for <see cref="RandomAccessCache{TValue}"/>.
    /// </summary>
    /// <param name="key">The key.</param>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct StringKey(string? key) : ISupplier<ReadOnlySpan<byte>>
    {
        private ReadOnlySpan<byte> Span => MemoryMarshal.AsReadOnlyBytes(key);

        /// <inheritdoc/>
        ReadOnlySpan<byte> ISupplier<ReadOnlySpan<byte>>.Invoke()
            => Span;

        /// <summary>
        /// Converts this key to <see cref="ReadOnlySpan{T}"/> over <see cref="char"/>.
        /// </summary>
        /// <param name="key">The key to convert.</param>
        /// <returns><see cref="ReadOnlySpan{T}"/> over <see cref="char"/> which represents the key.</returns>
        public static implicit operator ReadOnlySpan<byte>(StringKey key)
            => key.Span;
    }
}