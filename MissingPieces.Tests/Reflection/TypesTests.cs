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

		[Fact]
		public void ConstructorReflectionTest()
		{
			var ctor = typeof(string).GetConstructor(new[]{typeof(char), typeof(int)});
			Func<char, int, string> reflected = ctor.Bind<Func<char, int, string>>();
			Equal("ccc", reflected('c', 3));
		}
	}
}
