namespace DotNext.Runtime.CompilerServices;

internal sealed class Box<T>
    where T : struct
{
    internal readonly T Value;

    internal Box(T value) => Value = value;
}