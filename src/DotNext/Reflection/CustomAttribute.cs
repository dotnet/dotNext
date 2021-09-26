using System.Reflection;

namespace DotNext.Reflection;

/// <summary>
/// Contains static methods for retrieving custom attributes.
/// </summary>
public static class CustomAttribute
{
    /// <summary>
    /// Indicates whether one or more attributes of the specified type or of its derived types is applied to the member.
    /// </summary>
    /// <typeparam name="TAttribute">The type of custom attribute to search for. The search includes derived types.</typeparam>
    /// <param name="member">The member to inspect.</param>
    /// <param name="inherit"><see langword="true"/> to search this member's inheritance chain to find the attributes; otherwise, <see langword="false"/>. This parameter is ignored for properties and events.</param>
    /// <returns><see langword="true"/> if one or more instances of <typeparamref name="TAttribute"/> or any of its derived types is applied to the provided member; otherwise, <see langword="false"/>.</returns>
    public static bool IsDefined<TAttribute>(this ICustomAttributeProvider member, bool inherit = false)
        where TAttribute : Attribute
        => member.IsDefined(typeof(TAttribute), inherit);
}