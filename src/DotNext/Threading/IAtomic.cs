namespace DotNext.Threading;

internal interface IAtomic : ICloneable, IResettable
{
    object? Unwrap();
    new IAtomic Clone();
    object ICloneable.Clone() => Clone();
}