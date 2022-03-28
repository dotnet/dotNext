using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotNext.Reflection;

/// <summary>
/// Various extension methods for property reflection.
/// </summary>
public static class PropertyExtensions
{
    /// <summary>
    /// Checks whether the initialization of the property is allowed during object construction.
    /// </summary>
    /// <param name="property">The property to check.</param>
    /// <returns><see langword="true"/> if the property has <see langword="init"/> accessor; otherwise, <see langword="false"/>.</returns>
    /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/init">init (C# Reference)</seealso>
    public static bool IsExternalInit(this PropertyInfo property)
    {
        return property.SetMethod?.ReturnParameter.GetRequiredCustomModifiers() is { Length: > 0 } modifiers
            && Array.IndexOf(modifiers, typeof(IsExternalInit)) >= 0;
    }
}