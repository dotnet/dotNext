using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.Reflection
{
    [ExcludeFromCodeCoverage]
    public sealed class OperatorTests : Test
    {
        public class BaseClass
        {
            public static bool operator ==(int first, BaseClass second) => true;
            public static bool operator !=(int first, BaseClass second) => false;

            public static bool operator ==(BaseClass first, BaseClass second) => true;
            public static bool operator !=(BaseClass first, BaseClass second) => false;

            public static string operator +(BaseClass bc) => bc?.ToString();

            public override bool Equals(object obj) => obj is BaseClass;

            public override int GetHashCode() => GetType().GetHashCode();

            public static implicit operator string(BaseClass obj) => obj?.ToString();
        }

        public sealed class DerivedClass : BaseClass
        {
            public override bool Equals(object obj) => obj is DerivedClass;

            public override int GetHashCode() => GetType().GetHashCode();
        }

        [Fact]
        public void BinaryOperatorTest()
        {
            var op = Type<int>.Operator<BaseClass>.Require<bool>(BinaryOperator.Equal);
            True(op.Invoke(10, new BaseClass()));
            True(20 == new DerivedClass());
            var op2 = Type<int>.Operator<DerivedClass>.Require<bool>(BinaryOperator.Equal);
            True(op2.Invoke(20, new DerivedClass()));
        }

        [Fact]
        public void UnaryOperatorTest()
        {
            var unaryPlus = Type<DerivedClass>.Operator.Require<string>(UnaryOperator.Plus);
            var obj = new DerivedClass();
            Equal(obj.ToString(), unaryPlus.Invoke(obj));
            Operator<int, int> negate = Type<int>.Operator.Require(UnaryOperator.Negate, OperatorLookup.Predefined);
            Equal(-42, negate(42));
            //typecast
            var toLong = Type<byte>.Operator.Require<ulong>(UnaryOperator.Convert);
            Equal(42UL, toLong.Invoke(42));
            var toString = Type<DerivedClass>.Operator.Require<string>(UnaryOperator.Convert, OperatorLookup.Any);
            NotEmpty(toString.Invoke(new DerivedClass()));
            NotEmpty(Type<string>.Convert(new DerivedClass()));
        }
    }
}