using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Metaprogramming
{
    using Linq.Expressions;
    using static CodeGenerator;

    [ExcludeFromCodeCoverage]
    public sealed class BlockTests : Test
    {
        private sealed class DisposableClass : Disposable, IAsyncDisposable
        {
            public new bool IsDisposed => base.IsDisposed;

            ValueTask IAsyncDisposable.DisposeAsync()
                => new(Task.Run(Dispose));
        }

        private struct DisposableStruct
        {
            internal readonly StrongBox<bool> Disposed;

            internal DisposableStruct(StrongBox<bool> disposedFlag)
                => Disposed = disposedFlag;

            public void Dispose() => Disposed.Value = true;
        }

        private delegate void DisposeLambda(ref DisposableStruct value);

        [Fact]
        public static void DisposableStructTest()
        {
            var lambda = Lambda<DisposeLambda>(static fun =>
            {
                Using(fun[0], () =>
                {
                    InPlaceValue(42L);
                });
            })
           .Compile();
            var flag = new StrongBox<bool>(false);
            var value = new DisposableStruct(flag);
            False(flag.Value);
            lambda(ref value);
            True(flag.Value);
            Null(value.Disposed);
        }

        [Fact]
        public static void DisposableTest()
        {
            var lambda = Lambda<Func<DisposableClass, long>>(static fun =>
            {
                Using(fun[0], () =>
                {
                    InPlaceValue(42L);
                });
            })
           .Compile();
            var disposable = new DisposableClass();
            False(disposable.IsDisposed);
            Equal(42L, lambda(disposable));
            True(disposable.IsDisposed);
        }

        [Fact]
        public static void WithBlockTest()
        {
            var lambda = Lambda<Func<int, int>>(static fun =>
            {
                With((Expression)(fun[0].AsDynamic() + 10), static scopeVar =>
                {
                    Assign(scopeVar, scopeVar.AsDynamic() * 2);
                });
            })
            .Compile();
            Equal(28, lambda(4));
        }

        [Fact]
        public static void LockTest()
        {
            var lambda = Lambda<Action<StringBuilder>>(static fun =>
            {
                Lock(fun[0], () =>
                {
                    Call(fun[0], nameof(StringBuilder.Append), 'a'.Const());
                });
            })
            .Compile();
            var builder = new StringBuilder();
            lambda(builder);
            Equal("a", builder.ToString());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static async Task AwaitDisposableTest(bool configureAwait)
        {
            var lambda = AsyncLambda<Func<DisposableClass, Task<long>>>((fun, result) =>
            {
                AwaitUsing(fun[0], () =>
                {
                    Assign(result, 42L.Const());
                }, configureAwait);
            })
           .Compile();
            var disposable = new DisposableClass();
            False(disposable.IsDisposed);
            Equal(42L, await lambda(disposable));
            True(disposable.IsDisposed);
        }
    }
}