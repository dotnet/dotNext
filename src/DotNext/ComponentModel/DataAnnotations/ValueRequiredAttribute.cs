using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace DotNext.ComponentModel.DataAnnotations;

/// <summary>
/// Checks whether the data field of type <see cref="Optional{T}"/> has a value.
/// </summary>
/// <typeparam name="T">The type of <see cref="Optional{T}"/> value.</typeparam>
public sealed class ValueRequiredAttribute<T> : RequiredAttribute
{
    /// <summary>
    /// Specifies whether <see cref="Optional{T}"/> may contain <see langword="null"/> value.
    /// </summary>
    public bool AllowNull
    {
        get;
        set;
    }

    /// <inheritdoc/>
    public override bool IsValid(object? value)
    {
        switch (value)
        {
            case Optional<T>:
                ref var optional = ref Unsafe.Unbox<Optional<T>>(value);
                return optional.IsNull ? AllowNull : optional.HasValue;
            default:
                return false;
        }
    }
}