using System.Diagnostics.CodeAnalysis;

namespace DotNext.Reflection
{
    /// <summary>
    /// Represents static property or field value getter.
    /// </summary>
    /// <typeparam name="V">Type of the property of field.</typeparam>
    /// <returns>The value of the property of field.</returns>
    [return: MaybeNull]
    public delegate V MemberGetter<out V>();

    /// <summary>
    /// Represents static property or field setter.
    /// </summary>
    /// <typeparam name="V">Type of the property of field.</typeparam>
    /// <param name="value">The new value of the property or field.</param>
	public delegate void MemberSetter<in V>(V value);

    /// <summary>
    /// Represents instance field/property getter.
    /// </summary>
    /// <param name="this">This parameter.</param>
    /// <typeparam name="T">Declaring type.</typeparam>
    /// <typeparam name="V">Member type.</typeparam>
    /// <returns>Field value.</returns>
    [return: MaybeNull]
    public delegate V MemberGetter<T, out V>([DisallowNull]in T @this);

    /// <summary>
    /// Represents field setter.
    /// </summary>
    /// <param name="this">This parameter.</param>
    /// <param name="value">A value to set.</param>
    /// <typeparam name="T">Declaring type.</typeparam>
    /// <typeparam name="V">Member type.</typeparam>
    public delegate void MemberSetter<T, in V>([DisallowNull]in T @this, V value);
}
