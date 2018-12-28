using System;
using System.Reflection;
using Xunit;

namespace Cheats.Reflection
{
    public sealed class OperatorTests: Assert
    {
        public class BaseClass
		{
			public static bool operator ==(int first, BaseClass second) => true;
			public static bool operator !=(int first, BaseClass second) => false;

			public static bool operator ==(BaseClass first, BaseClass second) => true;
			public static bool operator !=(BaseClass first, BaseClass second) => false;

			public static string operator +(BaseClass bc) => bc?.ToString();

			public override bool Equals(object obj)
			{
				return base.Equals(obj);
			}

			public static implicit operator string(BaseClass obj) => obj?.ToString();
		}

		public sealed class DerivedClass: BaseClass
		{
			public override bool Equals(object obj)
			{
				return base.Equals(obj);
			}
		}

		[Fact]
		public void UnaryOperatorTest()
		{
			var unaryPlus = Type<DerivedClass>.UnaryOperator<string>.Require(UnaryOperator.Plus);
			var obj = new DerivedClass();
			Equal(obj.ToString(), unaryPlus.Invoke(obj));
			UnaryOperator<int, int>.Invoker negate = Type<int>.UnaryOperator<int>.Require(UnaryOperator.Negate);
			Equal(-42, negate(42));
			//typecast
			var toLong = Type<byte>.UnaryOperator<ulong>.Require(UnaryOperator.Convert);
			Equal(42UL, toLong.Invoke(42));
			var toString = Type<DerivedClass>.UnaryOperator<string>.Require(UnaryOperator.Convert);
			NotEmpty(toString.Invoke(new DerivedClass()));
			NotEmpty(Type<string>.Convert(new DerivedClass()));
		}
    }
}