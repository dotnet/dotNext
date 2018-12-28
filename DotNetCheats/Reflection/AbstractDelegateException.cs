using System;

namespace DotNetCheats.Reflection
{
	internal sealed class AbstractDelegateException<D>: GenericArgumentException<D>
		where D: Delegate
	{
		internal AbstractDelegateException()
			: base("Delegate type should not be abstract")
		{
		}
	}
}
