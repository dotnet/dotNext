using System;
using Xunit;

namespace Cheats.Reflection
{
	public sealed class TypesTest : Assert
	{
		[Fact]
		public void DelegateSignatureTest()
		{
			var signature = DelegateCheats.GetInvokeMethod<Func<int, string>>();
			NotNull(signature);
			Equal(typeof(int), signature.GetParameters()[0].ParameterType);
			Equal(typeof(string), signature.ReturnParameter.ParameterType);
		}
	}
}
