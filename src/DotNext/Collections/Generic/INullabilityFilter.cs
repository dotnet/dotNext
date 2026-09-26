using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Generic;

internal interface INullabilityFilter<in TInput, TOutput>
    where TOutput : notnull
{
    public static abstract bool TryGet(TInput? input, [NotNullWhen(true)] out TOutput? output);
}

[StructLayout(LayoutKind.Auto)]
internal readonly ref struct NotNullFilter<T> : INullabilityFilter<T?, T>
    where T : class
{
    static bool INullabilityFilter<T?, T>.TryGet(T? input, [NotNullWhen(true)] out T? output)
    {
        output = input;
        return input is not null;
    }
}

[StructLayout(LayoutKind.Auto)]
internal readonly ref struct HasValueFilter<T> : INullabilityFilter<T?, T>
    where T : struct
{
    static bool INullabilityFilter<T?, T>.TryGet(T? input, out T output)
    {
        output = input.GetValueOrDefault();
        return input.HasValue;
    }
}

[StructLayout(LayoutKind.Auto)]
internal readonly ref struct NotEmptyFilter<TMonad, T> : INullabilityFilter<TMonad, T>
    where T : notnull
    where TMonad : struct, IOptionMonad<T>
{
    static bool INullabilityFilter<TMonad, T>.TryGet(TMonad input, [NotNullWhen(true)] out T? output)
    {
        output = input.ValueOrDefault;
        return input.HasValue;
    }
}