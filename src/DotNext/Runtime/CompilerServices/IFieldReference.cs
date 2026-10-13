namespace DotNext.Runtime.CompilerServices;

/// <summary>
/// Provides field location in the memory.
/// </summary>
/// <typeparam name="T">Declaring type.</typeparam>
/// <typeparam name="TResult">The type of the field.</typeparam>
public interface IFieldReference<T, TResult>
    where T : allows ref struct
    where TResult : allows ref struct
{
    /// <summary>
    /// Obtains a reference to the field.
    /// </summary>
    /// <param name="value">The underlying value.</param>
    /// <returns>The field reference.</returns>
    public static abstract ref readonly TResult GetReference(in T value);
}