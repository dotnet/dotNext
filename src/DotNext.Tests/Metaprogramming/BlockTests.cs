using System;
using System.Text;
using Xunit;

namespace DotNext.Metaprogramming
{
    public sealed class BlockTests: Assert
    {
        private sealed class DisposableClass: Disposable
        {
            protected override void Dispose(bool disposing)
            {
                
            }

            public new bool IsDisposed => base.IsDisposed;
        }

        private struct DisposableStruct
        {
            internal readonly ValueType<bool> Disposed;

            internal DisposableStruct(ValueType<bool> disposedFlag)
                => Disposed = disposedFlag;

            public void Dispose() => Disposed.Value = true;
        }

        private delegate void DisposeLambda(ref DisposableStruct value);

        [Fact]
        public void DisposableStructTest()
        {
            var lambda = LambdaBuilder<DisposeLambda>.Build(fun =>
            {
                fun.Using(fun.Parameters[0], @using => @using.Constant(42L));
            })
           .Compile();
            var flag = new ValueType<bool>(false);
            var value = new DisposableStruct(flag);
            False(flag);
            lambda(ref value);
            True(flag);
            Null(value.Disposed);
        }

        [Fact]
        public void DisposableTest()
        {
            var lambda = LambdaBuilder<Func<DisposableClass, long>>.Build(fun =>
            {
                fun.Using(fun.Parameters[0], @using => @using.Constant(42L));
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
            var lambda = LambdaBuilder<Func<int, int>>.Build(fun =>
            {
                UniversalExpression arg = fun.Parameters[0];
                fun.With(arg + 10, scope => scope.Assign(scope.ScopeVar, scope.ScopeVar * 2));
            })
            .Compile();
            Equal(28, lambda(4));
        }

        [Fact]
        public void LockTest()
        {
            var lambda = LambdaBuilder<Action<StringBuilder>>.Build(fun =>
            {
                fun.Lock(fun.Parameters[0], @lock => 
                {
                    @lock.Call(fun.Parameters[0], nameof(StringBuilder.Append), 'a');
                });
            })
            .Compile();
            var builder = new StringBuilder();
            lambda(builder);
            Equal("a", builder.ToString());
        }
    }
}