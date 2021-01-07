using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Metaprogramming
{
    using Linq.Expressions;
    using static Collections.Generic.Sequence;
    using static CodeGenerator;

    [ExcludeFromCodeCoverage]
    public sealed class LoopTests : Test
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
                    Assign(result, result.AsDynamic() + item);
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
                    Assign(result, result.AsDynamic() + item);
                });
            })
            .Compile();
            Equal(10L, sum(new[] { 1L, 5L, 4L }));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task ForEachAsync(bool configureAwait)
        {
            var sum = AsyncLambda<Func<IAsyncEnumerable<long>, Task<long>>>((fun, result) =>
            {
                AwaitForEach(fun[0], item =>
                {
                    Assign(result, result.AsDynamic() + item);
                }, configureAwait: configureAwait);
            })
            .Compile();
            Equal(10L, await sum(new[] { 1L, 5L, 4L }.ToAsyncEnumerable()));
        }

        [Fact]
        public static async Task RegressionIssue46()
        {
            var result = AsyncLambda<Func<IAsyncEnumerable<char>, IAsyncEnumerable<char>, Task>>(fun =>
            {
                AwaitForEach(fun[0], ch =>
                {
                    WriteLine(ch);
                    AwaitForEach(fun[1], WriteLine);
                });
            }).Compile();
            
            var e = await ThrowsAsync<Exception>(() => result(GetData('1'), GetData('2')));
            Equal("Custom exception", e.Message);

            static async IAsyncEnumerable<char> GetData(char data)
            {
                await Task.Yield();
                yield return data;
                throw new Exception("Custom exception");
            }
        }

        [Fact]
        public static void DoWhileLoop()
        {
            var sum = Lambda<Func<long, long>>((fun, result) =>
            {
                var arg = fun[0];
                DoWhile((Expression)(arg.AsDynamic() > 0L), () =>
                {
                    Assign(result, arg.AsDynamic() + result);
                    Assign((ParameterExpression)arg, arg.AsDynamic() - 1L);
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
                var arg = fun[0];
                For(0L.Const(), i => i.AsDynamic() < arg, PostIncrementAssign, loopVar =>
                {
                    Assign(result, result.AsDynamic() + loopVar);
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
                var arg = fun[0];
                Assign(result, 1L.Const());
                While((Expression)(arg.AsDynamic() > 1L), () =>
                {
                    Assign(result, result.AsDynamic() * arg);
                    Assign((ParameterExpression)arg, arg.AsDynamic() - 1L);
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
                var arg = fun[0];
                Assign(result, 1L.Const());
                Loop(() =>
                {
                    If((Expression)(arg.AsDynamic() > 1L))
                        .Then(() =>
                        {
                            Assign(result, result.AsDynamic() * arg);
                            Assign((ParameterExpression)arg, arg.AsDynamic() - 1L);
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