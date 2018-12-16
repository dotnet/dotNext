using System;
using Xunit;

namespace MissingPieces.Reflection
{
	public sealed class TypesTest : Assert
	{
		[Fact]
		public void DelegateSignatureTest()
		{
			var signature = Delegates.GetInvokeMethod<Func<int, string>>();
			NotNull(signature);
			Equal(typeof(int), signature.GetParameters()[0].ParameterType);
			Equal(typeof(string), signature.ReturnParameter.ParameterType);
		}
	}
}
