namespace DotNext.Threading;

internal interface IAtomic : ICloneable, IResettable
{
    object? Unwrap();
}