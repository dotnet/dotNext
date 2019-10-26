using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Xunit;

namespace DotNext.Metaprogramming
{
    using Linq.Expressions;
    using static CodeGenerator;
    using U = Linq.Expressions.UniversalExpression;

    [ExcludeFromCodeCoverage]
    public sealed class LoopTests : Assert
    {
        public struct CustomEnumerator
        {
            private int counter;

            public bool MoveNext()
            {
                if (counter < 4)
                {
                    counter += 1;
                    return true;
                }
                else
                    return false;
            }

            public int Current => counter;
        }

        public sealed class CustomEnumerable
        {
            public CustomEnumerator GetEnumerator() => new CustomEnumerator();
        }

        [Fact]
        public static void CustomForEach()
        {
            var sum = Lambda<Func<CustomEnumerable, int>>((fun, result) =>
            {
                ForEach(fun[0], item =>
                {
                    Assign(result, (U)result + item);
                });
            })
            .Compile();
            Equal(10, sum(new CustomEnumerable()));
        }

        [Fact]
        public static void ArrayForEach()
        {
            var sum = Lambda<Func<long[], long>>((fun, result) =>
            {
                ForEach(fun[0], item =>
                {
                    Assign(result, (U)result + item);
                });
            })
            .Compile();
            Equal(10L, sum(new[] { 1L, 5L, 4L }));
        }

        [Fact]
        public static void DoWhileLoop()
        {
            var sum = Lambda<Func<long, long>>((fun, result) =>
            {
                var arg = (U)fun[0];
                DoWhile(arg > 0L, () =>
                {
                    Assign(result, arg + result);
                    Assign((ParameterExpression)arg, arg - 1L);
                });
            })
            .Compile();
            Equal(6, sum(3));
        }

        [Fact]
        public static void ForLoop()
        {
            var sum = Lambda<Func<long, long>>((fun, result) =>
            {
                var arg = (U)fun[0];
                For(0L.Const(), i => (U)i < arg, PostIncrementAssign, loopVar =>
                {
                    Assign(result, (U)result + loopVar);
                });
            })
            .Compile();
            Equal(6, sum(4));
        }

        [Fact]
        public static void FactorialUsingWhile()
        {
            var factorial = Lambda<Func<long, long>>((fun, result) =>
            {
                var arg = (U)fun[0];
                Assign(result, 1L.Const());
                While(arg > 1L, () =>
                {
                    Assign(result, (U)result * arg);
                    Assign((ParameterExpression)arg, arg - 1L);
                });
            })
            .Compile();
            Equal(6, factorial(3));
        }

        [Fact]
        public static void Factorial()
        {
            var factorial = Lambda<Func<long, long>>((fun, result) =>
            {
                var arg = (U)fun[0];
                Assign(result, 1L.Const());
                Loop(() =>
                {
                    If(arg > 1L)
                        .Then(() =>
                        {
                            Assign(result, (U)result * arg);
                            Assign((ParameterExpression)arg, arg - 1L);
                        })
                        .Else(Break)
                        .End();
                });
            })
            .Compile();
            Equal(6, factorial(3));
        }
    }
}