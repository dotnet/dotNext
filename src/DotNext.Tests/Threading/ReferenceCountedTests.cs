namespace DotNext.Threading;

public sealed class ReferenceCountedTests : Test
{
    [Fact]
    public static void ReleaseViaOwnership()
    {
        var obj = new MyDisposableObject();
        new ReferenceCountedOwner<MyDisposableObject>(obj).Dispose();
        True(obj.IsDisposed);
    }

    [Fact]
    public static void CaptureWithStrongRef()
    {
        var obj = new MyDisposableObject();
        ReferenceCounted<MyDisposableObject>.Scope accessor;
        var arc = new ReferenceCountedOwner<MyDisposableObject>(obj);
        accessor = arc.Acquire();
        arc.Dispose();
        
        True(accessor.IsValid);
        False(obj.IsDisposed);
        Same(obj, accessor.Value);
        Same(obj, accessor.ValueRef);
        
        accessor.Dispose();
        True(obj.IsDisposed);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    public static async Task ConcurrentAccess(int concurrentFlows)
    {
        using var counter = new AsyncCountdownEvent(concurrentFlows);
        var obj = new MyDisposableObject();
        ReferenceCounted<MyDisposableObject>.Scope accessor;
        using (var arc = new ReferenceCountedOwner<MyDisposableObject>(obj))
        {
            ReferenceCounted<MyDisposableObject> weak = arc;
            accessor = weak.Acquire();
            True(accessor.IsValid);
            
            for (var i = 0; i < concurrentFlows; i++)
            {
                ThreadPool.UnsafeQueueUserWorkItem(static args =>
                    {
                        using var strong = args.weak.Acquire();
                        True(strong.IsValid);
                        args.counter.Signal();
                    },
                    (weak, counter),
                    preferLocal: false);
            }
        }
        
        await counter.WaitAsync(TestToken);
        accessor.Dispose();
        
        await obj.Task.WaitAsync(TestToken);
    }

    [Fact]
    public static void InvalidScope()
    {
        Throws<InvalidOperationException>(static () => default(ReferenceCounted<MyDisposableObject>.Scope).Value);
        Throws<InvalidOperationException>(static () => default(ReferenceCounted<MyDisposableObject>.Scope).ValueRef);
        False(default(ReferenceCounted<MyDisposableObject>.Scope).IsValid);
    }

    [Fact]
    public static void AcquireWithExtension()
    {
        var expected = new MyDisposableObject();
        var arc = new ReferenceCountedOwner<MyDisposableObject>(expected);
        MyDisposableObject actual;
        using (arc.Acquire(out actual))
        {
            Same(expected, actual);
        }
        
        arc.Dispose();
        False(arc.Acquire(out actual).IsValid);
        Null(actual);
    }
}

file sealed class MyDisposableObject : TaskCompletionSource, IDisposable
{
    public bool IsDisposed => Task.IsCompletedSuccessfully;

    public void Dispose() => TrySetResult();
}