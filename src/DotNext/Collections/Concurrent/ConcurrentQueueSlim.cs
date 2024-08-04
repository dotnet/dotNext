using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Concurrent;

/// <summary>
/// Lightweight and lock-free alternative of <see cref="ConcurrentQueue{T}"/>.
/// </summary>
/// <remarks>
/// The queue doesn't implement <see cref="ICollection{T}"/> interface. It's designed for
/// pure producer-consumer scenario. However, this version has its own drabacks: every call of <see cref="Enqueue"/>
/// allocates a small object on the heap.
/// </remarks>
/// <typeparam name="T">The type of the elements contained in the queue.</typeparam>
public class ConcurrentQueueSlim<T>
{
    private Node? head, tail;

    /// <summary>
    /// Adds an object to the end of this queue.
    /// </summary>
    /// <param name="item">The object to add to the end of this queue.</param>
    public void Enqueue(T item)
    {
        var newNode = new Node(item);
        if (Interlocked.CompareExchange(ref tail, newNode, null) is { } currentTail)
        {
            EnqueueWithContention(currentTail, newNode);
        }
        else
        {
            head = newNode;
        }
    }
    
    private void EnqueueWithContention(Node currentTail, Node newNode)
    {
        var tmp = currentTail;
        do
        {
            currentTail = tmp;
            tmp = Interlocked.CompareExchange(ref currentTail.Next, newNode, null);
        } while (tmp is not null);

        // attempt to install a new tail. Do not retry if failed, competing thread installed more recent version of it
        Interlocked.CompareExchange(ref tail, newNode, currentTail);
    }

    /// <summary>
    /// Tries to remove and return the object at the beginning of this queue.
    /// </summary>
    /// <param name="result">The removed object.</param>
    /// <returns>
    /// <see langword="true" /> if an element was removed and returned from the beginning of this queue;
    /// otherwise <see langword="false"/>.
    /// </returns>
    public bool TryDequeue([MaybeNullWhen(false)] out T result)
    {
        if (Volatile.Read(in head) is { } currentHead)
            return TryDequeueWithContention(currentHead, out result);

        result = default;
        return false;
    }

    private bool TryDequeueWithContention(Node currentHead, [MaybeNullWhen(false)] out T value)
    {
        for (Node? newHead, tmp; !currentHead.TryRead(out value); currentHead = ReferenceEquals(tmp, currentHead) ? newHead : tmp)
        {
            newHead = currentHead.Next;

            if (newHead is null)
            {
                value = default;
                return false;
            }

            tmp = Interlocked.CompareExchange(ref head, newHead, currentHead);
            Debug.Assert(tmp is not null);
        }

        return true;
    }

    /// <summary>
    /// Gets consuming stream over the elements in this queue.
    /// </summary>
    /// <remarks>
    /// On every call of <see cref="ConsumingEnumerator.MoveNext()"/> the enumerator removes the element
    /// from the queue.
    /// </remarks>
    /// <returns>The consuming stream of elements.</returns>
    public ConsumingEnumerable Consume() => new(this);

    private IEnumerator<T> GetLegacyEnumerator()
    {
        while (TryDequeue(out var value))
        {
            yield return value;
        }
    }

    /// <summary>
    /// Represents consuming stream over the elements of the concurrent queue.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct ConsumingEnumerable : IEnumerable<T>
    {
        private readonly ConcurrentQueueSlim<T> queue;

        internal ConsumingEnumerable(ConcurrentQueueSlim<T> queue) => this.queue = queue;

        /// <summary>
        /// Gets consuming enumerator.
        /// </summary>
        /// <returns>The consuming enumerator.</returns>
        public ConsumingEnumerator GetEnumerator() => new(queue);

        /// <inheritdoc cref="IEnumerable{T}.GetEnumerator()"/>
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => queue.GetLegacyEnumerator();

        /// <inheritdoc cref="IEnumerable.GetEnumerator()"/>
        IEnumerator IEnumerable.GetEnumerator() => queue.GetLegacyEnumerator();
    }

    /// <summary>
    /// Represents consuming enumerator.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public struct ConsumingEnumerator
    {
        private readonly ConcurrentQueueSlim<T> queue;
        private T? current;

        internal ConsumingEnumerator(ConcurrentQueueSlim<T> queue) => this.queue = queue;

        /// <summary>
        /// Gets the removed element from the queue.
        /// </summary>
        public readonly T Current => current!;

        /// <summary>
        /// Removes an item from the queue.
        /// </summary>
        /// <returns>
        /// <see langword="true" /> if an element was removed and returned from the beginning of this queue;
        /// otherwise <see langword="false"/>.
        /// </returns>
        public bool MoveNext() => queue.TryDequeue(out current!);
    }

    private sealed class Node(T value)
    {
        internal Node? Next;
        private volatile uint visited;

        internal bool TryRead([MaybeNullWhen(false)] out T result)
        {
            if (Interlocked.Exchange(ref visited, 1U) is 0U)
            {
                result = value;
                if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                {
                    value = default!;
                }

                return true;
            }

            result = default;
            return false;
        }

        public override string? ToString() => value?.ToString();
    }
}