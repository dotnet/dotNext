namespace DotNext;

internal interface IReadOnlySpanList<T>
{
    int Count { get; }
    
    ReadOnlySpan<T> this[int index] { get; }
}