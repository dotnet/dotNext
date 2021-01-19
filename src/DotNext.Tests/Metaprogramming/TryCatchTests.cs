using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using Xunit;

namespace DotNext.Metaprogramming
{
    using Linq.Expressions;
    using static CodeGenerator;

    [ExcludeFromCodeCoverage]
    public sealed class TryCatchTests : Test
    {
        [Fact]
        public static void Fault()
        {
            var lambda = Lambda<Func<long, long, bool>>((fun, result) =>
            {
                var (arg1, arg2) = fun;
                Assign(result, true.Const());
                Try((Expression)(arg1.AsDynamic() / arg2))
                    .Fault(() => Assign(result, false.Const()))
                    .End();
            })
            .Compile();
            True(lambda(6, 3));
            Throws<DivideByZeroException>(() => lambda(6, 0));
        }

        [Fact]
        public static void Catch()
        {
            var lambda = Lambda<Func<long, long, bool>>((fun, result) =>
            {
                var (arg1, arg2) = fun;
                Assign(result, true.Const());
                Try((Expression)(arg1.AsDynamic() / arg2))
                    .Catch<DivideByZeroException>(() => Assign(result, false.Const()))
                    .End();
            })
            .Compile();

            True(lambda(6, 3));
            False(lambda(6, 0));
        }

        [Fact]
        public static void ReturnFromCatch()
        {
            var lambda = Lambda<Func<long, long, bool>>((fun, result) =>
            {
                var (arg1, arg2) = fun;
                Try((Expression)(arg1.AsDynamic() / arg2))
                    .Catch<DivideByZeroException>(() => Return(false.Const()))
                    .End();
                Return(true.Const());
            })
            .Compile();

            True(lambda(6, 3));
            False(lambda(6, 0));
        }

        [Fact]
        public static void CatchWithFilter()
        {
            var lambda = Lambda<Func<long, long, bool>>(static fun =>
            {
                var (arg1, arg2) = fun;
                Try(Expression.Block((Expression)(arg1.AsDynamic() / arg2), true.Const()))
                    .Catch(typeof(Exception), static e => e.InstanceOf<DivideByZeroException>(), static e => InPlaceValue(false))
                    .OfType<bool>()
                    .End();
            })
            .Compile();

            True(lambda(6, 3));
            False(lambda(6, 0));
        }
    }
}