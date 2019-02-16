using System;
using System.Collections.ObjectModel;

namespace DotNext.Reflection
{
	/// <summary>
	/// Indicates that requested constructor doesn't exist.
	/// </summary>
	public sealed class MissingConstructorException: ConstraintViolationException
	{
		public MissingConstructorException(Type target, params Type[] parameters)
			: base(target, ExceptionMessages.MissingCtor(target, parameters))
		{
			Parameters = Array.AsReadOnly(parameters);
		}

		public ReadOnlyCollection<Type> Parameters { get; }

		internal static MissingConstructorException Create<D>()
			where D: Delegate
		{
			var (parameters, target) = DelegateType.GetInvokeMethod<D>().Decompose(method => method.GetParameterTypes(), method => method.ReturnType);
			return new MissingConstructorException(target, parameters);
		}

		internal static MissingConstructorException Create<T, A>()
			where A: struct
			=> new MissingConstructorException(typeof(T), Signature.Reflect(typeof(A)).Parameters);
	}
}
