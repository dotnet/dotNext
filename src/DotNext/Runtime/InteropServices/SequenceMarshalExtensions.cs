using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.InteropServices;

/// <summary>
/// Extends <see cref="ReadOnlySeque"/>
/// </summary>
public static class SequenceMarshalExtensions
{
    /// <summary>
    /// Extends <see cref="SequenceMarshal"/> type.
    /// </summary>
    extension(SequenceMarshal)
    {
        /// <summary>
        /// Gets enumerator over all elements in the sequence.
        /// </summary>
        /// <param name="sequence">The sequence to be converted.</param>
        /// <typeparam name="T">The type of elements in the sequence.</typeparam>
        /// <returns>The enumerator over all elements in the sequence.</returns>
        public static IEnumerator<T> ToEnumerator<T>(in ReadOnlySequence<T> sequence)
        {
            return sequence.IsEmpty
                ? Enumerable.Empty<T>().GetEnumerator()
                : sequence.IsSingleSegment
                    ? MemoryMarshal.ToEnumerator(sequence.First)
                    : ToEnumeratorSlow(sequence.GetEnumerator());

            static IEnumerator<T> ToEnumeratorSlow(ReadOnlySequence<T>.Enumerator enumerator)
            {
                while (enumerator.MoveNext())
                {
                    var segment = enumerator.Current;

                    for (nint i = 0; i < segment.Length; i++)
                        yield return Unsafe.Add(ref MemoryMarshal.GetReference(segment.Span), i);
                }
            }
        }
    }
}