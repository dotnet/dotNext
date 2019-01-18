using System;
using Xunit;

namespace DotNext.Metaprogramming
{
    public sealed class LoopTests: Assert
    {
        public struct CustomEnumerator
        {
            private int counter;

            public bool MoveNext()
            {
                if(counter < 4)
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
        public void CustomForEachTest()
        {
            var sum = LambdaBuilder<Func<CustomEnumerable, int>>.Build(fun =>
            {
                fun.ForEach(fun.Parameters[0], loop =>
                {
                    loop.Assign(fun.Result, fun.Result + loop.Element);
                });
            })
            .Compile();
            Equal(10, sum(new CustomEnumerable()));
        }

        [Fact]
        public void ArrayForEachTest()
        {
            var sum = LambdaBuilder<Func<long[], long>>.Build(fun =>
            {
                fun.ForEach(fun.Parameters[0], loop =>
                {
                    loop.Assign(fun.Result, fun.Result + loop.Element);
                });
            })
            .Compile();
            Equal(10L, sum(new[] { 1L, 5L, 4L }));
        }

        [Fact]
        public void SumTest()
        {
            var sum = LambdaBuilder<Func<long, long>>.Build(fun =>
            {
                UniversalExpression arg = fun.Parameters[0];
                fun.DoWhile(arg > 0L, loop =>
                {
                    loop.Assign(fun.Result, fun.Result + arg);
                    loop.Assign(arg, arg - 1L);
                });
            })
            .Compile();
            Equal(6, sum(3));
        }

        [Fact]
        public void Sum2Test()
        {
            var sum = LambdaBuilder<Func<long, long>>.Build(fun =>
            {
                UniversalExpression arg = fun.Parameters[0];
                fun.For(0L, i => i < arg, loop =>
                {
                    loop.Assign(fun.Result, fun.Result + (UniversalExpression)loop.LoopVar);
                    loop.StartIteratorBlock();
                    loop.Assign(loop.LoopVar, loop.LoopVar + (UniversalExpression)1L);
                });
            })
            .Compile();
            Equal(6, sum(4));
        }

        [Fact]
        public void FactorialTest()
        {
            var factorial = LambdaBuilder<Func<long, long>>.Build(fun => 
            {
                UniversalExpression arg = fun.Parameters[0];
                fun.Assign(fun.Result, 1L);
                fun.While(arg > 1L, loop =>
                {
                    loop.Assign(fun.Result, fun.Result * arg);
                    loop.Assign(arg, arg - 1L);
                });
            })
            .Compile();
            Equal(6, factorial(3));
        }

        [Fact]
        public void Factorial2Test()
        {
            var factorial = LambdaBuilder<Func<long, long>>.Build(fun =>
            {
                UniversalExpression arg = fun.Parameters[0];
                fun.Assign(fun.Result, 1L);
                fun.Loop(loop =>
                {
                    loop.If(arg > 1L)
                        .Then(then =>
                        {
                            then.Assign(fun.Result, fun.Result * arg);
                            then.Assign(arg, arg - 1L);
                        })
                        .Else(@else => @else.Break(loop))
                        .End();
                });
            })
            .Compile();
            Equal(6, factorial(3));
        }
    }
}