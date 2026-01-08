namespace DotNext;

public sealed class DisposableTests : Test
{
    private sealed class DisposableObject : Disposable, IAsyncDisposable
    {
        public new bool IsDisposed => base.IsDisposed;

        protected override ValueTask DisposeAsyncCore()
        {
            True(IsDisposing);
            True(IsDisposingOrDisposed);
            return ValueTask.CompletedTask;
        }

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