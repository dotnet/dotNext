using System;
using System.Collections.Generic;
using Xunit;

namespace DotNext.Reflection
{
	public sealed class TypesTest : Assert
	{
        public sealed class MyList: List<string>
        {

        }

		[Fact]
		public void DelegateSignatureTest()
		{
			var signature = Delegates.GetInvokeMethod<Func<int, string>>();
			NotNull(signature);
			Equal(typeof(int), signature.GetParameters()[0].ParameterType);
			Equal(typeof(string), signature.ReturnParameter.ParameterType);
		}

        [Fact]
        public void IsGenericInstanceOfTest()
        {
            True(typeof(Func<string>).IsGenericInstanceOf(typeof(Func<>)));
            False(typeof(Func<string>).IsGenericInstanceOf(typeof(Func<int>)));
            True(typeof(List<int>).IsGenericInstanceOf(typeof(List<>)));
            True(typeof(MyList).IsGenericInstanceOf(typeof(IEnumerable<>)));
        }

        [Fact]
        public void CollectionElementTest()
        {
            Equal(typeof(string), typeof(MyList).GetCollectionElementType(out var enumerable));
            Equal(typeof(IEnumerable<string>), enumerable);
        }
	}
}
