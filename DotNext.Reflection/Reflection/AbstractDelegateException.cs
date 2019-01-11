using System;

namespace Cheats.Reflection
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
