using System.Reflection;
using System.Runtime.InteropServices;

namespace DotNext.Reflection;

using Runtime.CompilerServices;

/// <summary>
/// Extends <see cref="FieldInfo"/> class.
/// </summary>
public static class FieldExtensions
{
    [ThreadStatic] private static FieldInfo[]? threadCache;

    /// <summary>
    /// Gets value of the field.
    /// </summary>
    /// <param name="field">The field reference.</param>
    /// <param name="owner">The field owner.</param>
    /// <typeparam name="T">The type of the field.</typeparam>
    /// <returns>The field location.</returns>
    public static ref T GetValue<T>(this FieldInfo field, object owner)
    {
        ref var fieldRef = ref MemoryMarshal.GetArrayDataReference(threadCache ??= new FieldInfo[1]);
        fieldRef = field;
        return ref TypedReference.MakeTypedReference(owner, threadCache).AsRef<T>();
    }
}