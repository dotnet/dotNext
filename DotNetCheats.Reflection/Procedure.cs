namespace Cheats
{
	/// <summary>
	/// Represents a static procedure with arbitrary number of arguments
	/// allocated on the stack.
	/// </summary>
	/// <param name="arguments">Procedure arguments in the form of public structure fields.</param>
	/// <typeparam name="A">Type of structure with procedure arguments allocated on the stack.</typeparam>
	public delegate void Procedure<A>(in A arguments)
		where A : struct;

	/// <summary>
	/// Represents an instance procedure with arbitrary number of arguments
	/// allocated on the stack.
	/// </summary>
	/// <param name="this">Hidden This parameter.</param>
	/// <param name="arguments">Procedure arguments in the form of public structure fields.</param>
	/// <typeparam name="T">Type of instance to be passed into underlying method.</typeparam>
	/// <typeparam name="A">Type of structure with procedure arguments allocated on the stack.</typeparam>
	public delegate void Procedure<T, A>(in T @this, in A arguments);

	public static class Procedure
	{
		public static A ArgList<A>(this Procedure<A> procedure)
            where A: struct
            => new A();
        
        public static A ArgList<T, A>(this Procedure<T, A> procedure)
            where A: struct
            => new A();
	}
}
