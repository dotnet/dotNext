namespace DotNext.Collections.Generic;

/// <summary>
/// Various methods to work with classes implementing <see cref="IEnumerable{T}"/> interface.
/// </summary>
public static partial class Enumerator
{
    /// <summary>
    /// Extends <see cref="IEnumerator{T}"/> type.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    extension<T>(IEnumerator<T>)
    {
        /// <summary>
        /// Bypasses a specified number of elements in a sequence.
        /// </summary>
        /// <param name="enumerator">Enumerator to modify. Cannot be <see langword="null"/>.</param>
        /// <param name="count">The number of elements to skip.</param>
        /// <returns><see langword="true"/>, if current element is available; otherwise, <see langword="false"/>.</returns>
        public static bool operator << (IEnumerator<T> enumerator, int count)
        {
            for (; count > 0; count--)
            {
                if (!enumerator.MoveNext())
                    return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Bypasses a specified number of elements in a sequence.
    /// </summary>
    /// <typeparam name="TEnumerator">The type of the sequence.</typeparam>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    /// <param name="enumerator">Enumerator to modify.</param>
    /// <param name="count">The number of elements to skip.</param>
    /// <returns><see langword="true"/>, if current element is available; otherwise, <see langword="false"/>.</returns>
    public static bool Skip<TEnumerator, T>(this ref TEnumerator enumerator, int count)
        where TEnumerator : struct, IEnumerator<T>, allows ref struct
    {
        for (; count > 0; count--)
        {
            if (!enumerator.MoveNext())
                return false;
        }

        return true;
    }

    /// <summary>
    /// Limits the number of the elements in the sequence.
    /// </summary>
    /// <typeparam name="T">The type of items in the sequence.</typeparam>
    /// <param name="enumerator">The sequence of the elements.</param>
    /// <param name="count">The maximum number of the elements in the returned sequence.</param>
    /// <param name="leaveOpen"><see langword="false"/> to dispose <paramref name="enumerator"/>; otherwise, <see langword="true"/>.</param>
    /// <returns>The enumerator which is limited by count.</returns>
    public static LimitedEnumerator<T> Limit<T>(this IEnumerator<T> enumerator, int count, bool leaveOpen = false)
        => new(enumerator, count, leaveOpen);
    
    /// <summary>
    /// Extends <see cref="IAsyncEnumerator{T}"/> type.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    extension<T>(IAsyncEnumerator<T> e)
    {
        /// <summary>
        /// Bypasses a specified number of elements in a sequence.
        /// </summary>
        /// <param name="enumerator">Enumerator to modify. Cannot be <see langword="null"/>.</param>
        /// <param name="count">The number of elements to skip.</param>
        /// <returns><see langword="true"/>, if current element is available; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask<bool> operator << (IAsyncEnumerator<T> enumerator, int count)
            => enumerator.SkipAsync(count);

        private async ValueTask<bool> SkipAsync(int count)
        {
            for (; count > 0; count--)
            {
                if (!await e.MoveNextAsync().ConfigureAwait(false))
                    return false;
            }

            return true;
        }
    }
}