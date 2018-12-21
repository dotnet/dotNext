using System;
using Xunit;

namespace MissingPieces.Metaprogramming
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
			Func<DerivedClass, string> unaryPlus = Type<DerivedClass>.Operator<Func<DerivedClass, string>>.Unary.GetOrNull(UnaryOperator.Plus);
			var obj = new DerivedClass();
			Equal(obj.ToString(), unaryPlus(obj));
			Func<int, int> negate = Type<int>.Operator<Func<int, int>>.Unary.GetOrNull(UnaryOperator.Negate);
			Equal(-42, negate(42));
		}
    }
}