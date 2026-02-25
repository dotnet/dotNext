namespace DotNext;

using Buffers;

/// <summary>
/// Provides extension methods for type <see cref="Span{T}"/> and <see cref="ReadOnlySpan{T}"/>.
/// </summary>
public static partial class Span
{
    internal static MemoryOwner<T> Concat<T, TList>(this ref TList list, MemoryAllocator<T>? allocator)
        where TList : struct, IReadOnlySpanList<T>, allows ref struct
    {
        var length = 0UL;
        for (var i = 0; i < list.Count; i++)
        {
            var span = list[i];
            length += (uint)span.Length;
        }

        if (length > (uint)Array.MaxLength)
            throw new OutOfMemoryException();

        MemoryOwner<T> buffer;
        if (length is 0UL)
        {
            buffer = default;
        }
        else
        {
            buffer = allocator.DefaultIfNull.AllocateExactly((int)length);
            var writer = new SpanWriter<T>(buffer.Span);
            for (var i = 0; i < list.Count; i++)
            {
                writer += list[i];
            }
        }

        return buffer;
    }
    
    internal interface IReadOnlySpanList<T>
    {
        int Count { get; }
        
        ReadOnlySpan<T> this[int index] { get; }
    }
}