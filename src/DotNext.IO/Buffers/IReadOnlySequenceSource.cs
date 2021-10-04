using System.Buffers;

namespace DotNext.Buffers;

/// <summary>
/// Represents disposable source of <see cref="ReadOnlySequence{T}"/>.
/// </summary>
/// <typeparam name="T">The type of the elements in the sequence.</typeparam>
public interface IReadOnlySequenceSource<T> : IDisposable, ISupplier<ReadOnlySequence<T>>
{
    /// <summary>
    /// Gets the sequence of elements associated with this source.
    /// </summary>
    /// <remarks>
    /// The sequence is no longer valid after calling of <see cref="IDisposable.Dispose"/> method.
    /// </remarks>
    /// <value>The sequence of elements associated with this source.</value>
    ReadOnlySequence<T> Sequence { get; }

    /// <inheritdoc />
    ReadOnlySequence<T> ISupplier<ReadOnlySequence<T>>.Invoke() => Sequence;
}