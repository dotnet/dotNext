using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Generic;

/// <summary>
/// Represents lazily converted read-only list.
/// </summary>
/// <typeparam name="TInput">Type of items in the source list.</typeparam>
/// <typeparam name="TOutput">Type of items in the converted list.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly struct ReadOnlyListView<TInput, TOutput> : IReadOnlyList<TOutput>, IEquatable<ReadOnlyListView<TInput, TOutput>>
{
    private readonly IReadOnlyList<TInput>? source;
    private readonly Func<TInput, TOutput> mapper;

    /// <summary>
    /// Initializes a new lazily converted view.
    /// </summary>
    /// <param name="list">Read-only list to convert.</param>
    /// <param name="mapper">List items converter.</param>
    public ReadOnlyListView(IReadOnlyList<TInput> list, Func<TInput, TOutput> mapper)
    {
        source = list ?? throw new ArgumentNullException(nameof(list));
        this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    /// <summary>
    /// Initializes a new lazily converted view.
    /// </summary>
    /// <param name="list">Read-only list to convert.</param>
    /// <param name="mapper">List items converter.</param>
    public ReadOnlyListView(IReadOnlyList<TInput> list, Converter<TInput, TOutput> mapper)
        : this(list, Unsafe.As<Func<TInput, TOutput>>(mapper))
    {
    }

    /// <summary>
    /// Gets item at the specified position.
    /// </summary>
    /// <param name="index">Zero-based index of the item.</param>
    /// <returns>Converted item at the specified position.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of range.</exception>
    public TOutput this[int index]
    {
        get
        {
            if (source is null)
                throw new ArgumentOutOfRangeException(nameof(index));

            return mapper.Invoke(source[index]);
        }
    }

    /// <summary>
    /// Count of items in the list.
    /// </summary>
    public int Count => source?.Count ?? 0;

    /// <summary>
    /// Returns enumerator over converted items.
    /// </summary>
    /// <returns>The enumerator over converted items.</returns>
    public IEnumerator<TOutput> GetEnumerator()
        => source is null || mapper is null || source.Count == 0 ? Sequence.GetEmptyEnumerator<TOutput>() : source.Select(mapper).GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private bool Equals(in ReadOnlyListView<TInput, TOutput> other)
        => ReferenceEquals(source, other.source) && mapper == other.mapper;

    /// <summary>
    /// Determines whether two converted lists are same.
    /// </summary>
    /// <param name="other">Other list to compare.</param>
    /// <returns><see langword="true"/> if this view wraps the same source list and contains the same converter as other view; otherwise, <see langword="false"/>.</returns>
    public bool Equals(ReadOnlyListView<TInput, TOutput> other) => Equals(in other);

    /// <summary>
    /// Returns hash code for the this list.
    /// </summary>
    /// <returns>The hash code of this list.</returns>
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(source) ^ mapper.GetHashCode();

    /// <summary>
    /// Determines whether two converted lists are same.
    /// </summary>
    /// <param name="other">Other list to compare.</param>
    /// <returns><see langword="true"/> if this collection wraps the same source collection and contains the same converter as other collection; otherwise, <see langword="false"/>.</returns>
    public override bool Equals(object? other)
        => other is ReadOnlyListView<TInput, TOutput> view ? Equals(in view) : Equals(source, other);

    /// <summary>
    /// Determines whether two views are same.
    /// </summary>
    /// <param name="first">The first list view to compare.</param>
    /// <param name="second">The second list view to compare.</param>
    /// <returns><see langword="true"/> if the first view wraps the same source collection and contains the same converter as the second view; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(in ReadOnlyListView<TInput, TOutput> first, in ReadOnlyListView<TInput, TOutput> second)
        => first.Equals(in second);

    /// <summary>
    /// Determines whether two views are not same.
    /// </summary>
    /// <param name="first">The first list view to compare.</param>
    /// <param name="second">The second list view to compare.</param>
    /// <returns><see langword="true"/> if the first view wraps the different source collection and contains the different converter as the second view; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(in ReadOnlyListView<TInput, TOutput> first, in ReadOnlyListView<TInput, TOutput> second)
        => !first.Equals(in second);
}