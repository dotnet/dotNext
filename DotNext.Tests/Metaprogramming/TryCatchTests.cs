using System;
using System.Linq.Expressions;
using Xunit;

namespace DotNext.Metaprogramming
{
    public sealed class TryCatchTests: Assert
    {
        [Fact]
        public void DivisionByZeroTest()
        {
            var lambda = LambdaBuilder<Func<long, long, bool>>.Build(fun =>
            {
                UniversalExpression param1 = fun.Parameters[0], param2 = fun.Parameters[1];
                fun.Assign(fun.Result, true);
                fun.Try(param1 / param2)
                    .Fault(fault => fault.Assign(fun.Result, false))
                    .End();
            })
            .Compile();
            True(lambda(6, 3));
            Throws<DivideByZeroException>(() => lambda(6, 0));
        }

        [Fact]
        public void DivisionByZeroTest2()
        {
            var lambda = LambdaBuilder<Func<long, long, bool>>.Build(fun =>
            {
                UniversalExpression param1 = fun.Parameters[0], param2 = fun.Parameters[1];
                fun.Assign(fun.Result, true);
                fun.Try(param1 / param2)
                    .Catch<DivideByZeroException>(@catch => @catch.Assign(fun.Result, false))
                    .End();
            })
            .Compile();

            True(lambda(6, 3));
            False(lambda(6, 0));
        }

        [Fact]
        public void DivisionByZeroTest3()
        {
            var lambda = LambdaBuilder<Func<long, long, bool>>.Build(fun =>
            {
                UniversalExpression param1 = fun.Parameters[0], param2 = fun.Parameters[1];
                fun.Try(param1 / param2)
                    .Catch<DivideByZeroException>(@catch => @catch.Return(fun, false))
                    .End();
                fun.Return(true);
            })
            .Compile();

            True(lambda(6, 3));
            False(lambda(6, 0));
        }

        [Fact]
        public void DivisionByZeroTest4()
        {
            var lambda = LambdaBuilder<Func<long, long, bool>>.Build(fun =>
            {
                UniversalExpression param1 = fun.Parameters[0], param2 = fun.Parameters[1];
                fun.Try(Expression.Block(param1 / param2, true.AsConst()))
                    .Catch(typeof(Exception), e => e.InstanceOf<DivideByZeroException>(), e => false)
                    .OfType<bool>()
                    .End();
            })
            .Compile();

            True(lambda(6, 3));
            False(lambda(6, 0));
        }
    }
}