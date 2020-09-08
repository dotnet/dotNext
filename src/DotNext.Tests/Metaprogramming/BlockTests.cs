using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;

namespace DotNext.Metaprogramming
{
    using Linq.Expressions;
    using static CodeGenerator;

    [ExcludeFromCodeCoverage]
    public sealed class BlockTests : Test
    {
        private sealed class DisposableClass : Disposable
        {
            public new bool IsDisposed => base.IsDisposed;
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
        public void DisposableStructTest()
        {
            var lambda = Lambda<DisposeLambda>(fun =>
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
        public void DisposableTest()
        {
            var lambda = Lambda<Func<DisposableClass, long>>(fun =>
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
        public void WithBlockTest()
        {
            var lambda = Lambda<Func<int, int>>(fun =>
            {
                With((Expression)(fun[0].AsDynamic() + 10), scopeVar =>
                {
                    Assign(scopeVar, scopeVar.AsDynamic() * 2);
                });
            })
            .Compile();
            Equal(28, lambda(4));
        }

        [Fact]
        public void LockTest()
        {
            var lambda = Lambda<Action<StringBuilder>>(fun =>
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
    }
}