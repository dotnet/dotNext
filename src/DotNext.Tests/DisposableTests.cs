using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
    public sealed class DisposableTests : Test
    {
        public struct DisposableStruct : IDisposable
        {
            public int Disposed;

            void IDisposable.Dispose() => Disposed += 1;
        }

        public struct DisposableStruct2
        {
            public int Disposed;

            public void Dispose() => Disposed += 1;
        }

        [Fact]
        public static void IDisposableStruct()
        {
            var s = new DisposableStruct();
            Disposable<DisposableStruct>.Dispose(s);
            Equal(1, s.Disposed);
            Disposable<DisposableStruct>.Dispose(s);
            Equal(2, s.Disposed);
        }

        [Fact]
        public static void DisposePattern()
        {
            var s = new DisposableStruct2();
            Disposable<DisposableStruct2>.Dispose(s);
            Equal(1, s.Disposed);
            Disposable<DisposableStruct2>.Dispose(s);
            Equal(2, s.Disposed);
        }

        [Fact]
        public static void MemoryStreamTest()
        {
            var ms = new MemoryStream(new byte[] { 1, 2, 3 });
            Disposable<MemoryStream>.Dispose(ms);
            Throws<ObjectDisposedException>(() => ms.ReadByte());
        }

        private sealed class DisposableObject : Disposable, IAsyncDisposable
        {
            public new bool IsDisposed => base.IsDisposed;

            ValueTask IAsyncDisposable.DisposeAsync() => DisposeAsync();
        }

        [Fact]
        public static void DisposeMany()
        {
            var obj1 = new DisposableObject();
            var obj2 = new DisposableObject();
            False(obj1.IsDisposed);
            False(obj2.IsDisposed);
            Disposable.Dispose(obj1, obj2, null);
            True(obj1.IsDisposed);
            True(obj2.IsDisposed);
        }

        [Fact]
        public static async Task DisposeManyAsync()
        {
            var obj1 = new DisposableObject();
            var obj2 = new DisposableObject();
            False(obj1.IsDisposed);
            False(obj2.IsDisposed);
            await Disposable.DisposeAsync(obj1, obj2, null);
            True(obj1.IsDisposed);
            True(obj2.IsDisposed);
        }
    }
}