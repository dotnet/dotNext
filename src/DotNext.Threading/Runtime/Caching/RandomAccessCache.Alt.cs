using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static System.Threading.Timeout;

namespace DotNext.Runtime.Caching;

using CompilerServices;
using Threading;

partial class RandomAccessCache<TKey, TValue>
{
    /// <summary>
    /// Tries to read the cached record by using alternative key representation.
    /// </summary>
    /// <remarks>
    /// The cache guarantees that the value cannot be evicted concurrently with the session. However,
    /// the value can be evicted immediately after. The caller must dispose session.
    /// </remarks>
    /// <param name="key">The key of the cache record.</param>
    /// <param name="session">A session that can be used to read the cached record.</param>
    /// <returns><see langword="true"/> if the record is available for reading and the session is active; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="InvalidOperationException"><see cref="KeyComparer"/> doesn't implement <see cref="IAlternateEqualityComparer{TAlternate,T}"/>.</exception>
    public bool TryRead<TAlternate>(TAlternate key, out ReadSession session)
        where TAlternate : notnull, allows ref struct
        => TryRead(keyComparer as IAlternateEqualityComparer<TAlternate, TKey> ??
                   throw new InvalidOperationException(ExceptionMessages.UnsupportedAltCacheView),
            key,
            out session);
    
    private bool TryRead<TAlternate>(IAlternateEqualityComparer<TAlternate, TKey> comparer, TAlternate key, out ReadSession session)
        where TAlternate : notnull, allows ref struct
        => TryRead<AlternateRefSelector<TAlternate>>(new(key, comparer),
            comparer.GetHashCode(key),
            out session);

    /// <summary>
    /// Gets the alternate view of this cache.
    /// </summary>
    /// <typeparam name="TAlternate">The alternate key representation.</typeparam>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"><typeparamref name="TAlternate"/> key type is not supported.</exception>
    public AlternateLookup<TAlternate> GetAlternateLookup<TAlternate>()
        where TAlternate : notnull
        => keyComparer is IAlternateEqualityComparer<TAlternate, TKey>
            ? new(this)
            : throw new InvalidOperationException(ExceptionMessages.UnsupportedAltCacheView);

    /// <summary>
    /// Provides a type that can be used to perform operations on a cache using alternate key representation. 
    /// </summary>
    /// <typeparam name="TAlternate">The alternate key representation.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct AlternateLookup<TAlternate>
        where TAlternate : notnull
    {
        private readonly RandomAccessCache<TKey, TValue> cache;

        internal AlternateLookup(RandomAccessCache<TKey, TValue> cache)
        {
            Debug.Assert(cache.keyComparer is IAlternateEqualityComparer<TAlternate, TKey>);

            this.cache = cache;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private IAlternateEqualityComparer<TAlternate, TKey> Comparer
            => Unsafe.As<IAlternateEqualityComparer<TAlternate, TKey>>(cache.keyComparer!);

        /// <summary>
        /// Tries to read the cached record by using alternative key representation.
        /// </summary>
        /// <remarks>
        /// The cache guarantees that the value cannot be evicted concurrently with the session. However,
        /// the value can be evicted immediately after. The caller must dispose session.
        /// </remarks>
        /// <param name="key">The key of the cache record.</param>
        /// <param name="session">A session that can be used to read the cached record.</param>
        /// <returns><see langword="true"/> if the record is available for reading and the session is active; otherwise, <see langword="false"/>.</returns>
        public bool TryRead(TAlternate key, out ReadSession session)
            => cache.TryRead(Comparer, key, out session);

        /// <summary>
        /// Invalidates the cache record associated with the specified key.
        /// </summary>
        /// <param name="key">The key of the cache record to be removed.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns><see langword="true"/> if the cache record associated with <paramref name="key"/> is removed successfully; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ObjectDisposedException">The cache is disposed.</exception>
        public ValueTask<bool> InvalidateAsync(TAlternate key, CancellationToken token = default)
        {
            var comparer = Comparer;
            return cache.InvalidateAsync<Selector>(
                new(key, comparer),
                comparer.GetHashCode(key),
                InfiniteTimeSpan,
                token);
        }

        /// <summary>
        /// Tries to invalidate cache record associated with the provided key.
        /// </summary>
        /// <param name="key">The key of the cache record to be removed.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>
        /// The session that can be used to read the removed cache record;
        /// or <see langword="null"/> if there is no record associated with <paramref name="key"/>.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ObjectDisposedException">The cache is disposed.</exception>
        public ValueTask<ReadSession?> TryRemoveAsync(TAlternate key, CancellationToken token = default)
        {
            var comparer = Comparer;
            return cache.TryRemoveAsync<Selector>(
                new(key, comparer),
                comparer.GetHashCode(key),
                InfiniteTimeSpan,
                token);
        }

        /// <summary>
        /// Opens a session that can be used to modify the value associated with the key.
        /// </summary>
        /// <param name="key">The key of the cache record.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The session that can be used to read or modify the cache record.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ObjectDisposedException">The cache is disposed.</exception>
        public ValueTask<ReadWriteSession> ChangeAsync(TAlternate key, CancellationToken token = default)
        {
            var comparer = Comparer;
            return cache.ChangeAsync<Selector>(
                new(key, comparer),
                comparer.GetHashCode(key),
                Timeout.Infinite,
                token);
        }

        /// <summary>
        /// Replaces the cache entry associated with the specified alternate key.
        /// </summary>
        /// <param name="key">The key associated with the cache entry.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The session that can be used to modify the cache record.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ObjectDisposedException">The cache is disposed.</exception>
        public ValueTask<ReadWriteSession> ReplaceAsync(TAlternate key, CancellationToken token = default)
        {
            var comparer = Comparer;
            return cache.ReplaceAsync<Selector>(
                new(key, comparer),
                comparer.GetHashCode(key),
                Timeout.Infinite,
                token);
        }

        [StructLayout(LayoutKind.Auto)]
        private readonly struct Selector(TAlternate expected, IAlternateEqualityComparer<TAlternate, TKey> comparer)
            : IEquatable<TKey>, ISupplier<TKey>
        {
            bool IEquatable<TKey>.Equals(TKey? actual) => comparer.Equals(expected, actual!);

            TKey ISupplier<TKey>.Invoke() => comparer.Create(expected);
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct AlternateRefSelector<TAlternate>(TAlternate expected, IAlternateEqualityComparer<TAlternate, TKey> comparer)
        : IEquatable<TKey>, ISupplier<TKey>
        where TAlternate : notnull, allows ref struct
    {
        private readonly TAlternate expected = expected;
        
        bool IEquatable<TKey>.Equals(TKey? actual) => comparer.Equals(expected, actual!);

        TKey ISupplier<TKey>.Invoke() => comparer.Create(expected);

        void IFunctional.DynamicInvoke(scoped ref readonly Variant args, int count, scoped Variant result)
            => throw new NotSupportedException();
    }
}