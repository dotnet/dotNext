using System;
using System.Reflection;

namespace DotNetCheats.Reflection
{
	/// <summary>
	/// Represents typed access to type constructor.
	/// </summary>
	/// <typeparam name="D">Type of delegate representing constructor signature.</typeparam>
	public interface IConstructor<out D>: IMethod<ConstructorInfo, D>
		where D: Delegate
	{
	}
}
