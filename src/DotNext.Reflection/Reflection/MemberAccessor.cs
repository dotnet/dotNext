namespace DotNext.Reflection
{
	public delegate V MemberGetter<out V>();

	public delegate void MemberSetter<in V>(V value);

	/// <summary>
	/// Represents instance field/property getter.
	/// </summary>
	/// <param name="this">This parameter.</param>
	/// <typeparam name="T">Declaring type.</typeparam>
	/// <returns>Field value.</returns>
	public delegate V MemberGetter<T, out V>(in T @this);

	/// <summary>
	/// Represents field setter.
	/// </summary>
	/// <param name="this">This parameter.</param>
	/// <param name="value">A value to set.</param>
	/// <typeparam name="T">Declaring type.</typeparam>
	public delegate void MemberSetter<T, in V>(in T @this, V value);
}
