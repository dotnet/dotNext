using System.Runtime.CompilerServices;

namespace DotNext.Threading;

public sealed class EpochTests : Test
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void DeferredReclamation(bool throwOnFirstException)
    {
        var state = new StrongBox<int>();
        var epoch = new Epoch();

        using (var region = epoch.Enter())
        {
            region.Defer(() => state.Value = 42);
            True(region.Reclaim().IsEmpty);
        }

        Equal(0, state.Value);

        epoch.Enter().Dispose();
        Equal(0, state.Value);

        using (var region = epoch.Enter())
        {
            var bin = region.Reclaim(drainGlobalCache: true);
            False(bin.IsEmpty);
            bin.Clear(throwOnFirstException);
        }
        
        Equal(42, state.Value);
    }
    
    [Fact]
    public static void DiscardableReclamation()
    {
        var state = new DiscardableObject();
        var epoch = new Epoch();

        using (var region = epoch.Enter())
        {
            region.RegisterForDiscard(state);
            True(region.Reclaim().IsEmpty);
        }

        False(state.IsDisposed);

        using (var region = epoch.Enter())
        {
            var bin = region.Reclaim(drainGlobalCache: true);
            False(bin.IsEmpty);
            bin.Clear();
        }
        
        True(state.IsDisposed);
    }
    
    [Fact]
    public static void DisposableReclamation()
    {
        var state = new DisposableObject();
        var epoch = new Epoch();

        using (var region = epoch.Enter())
        {
            region.RegisterForDispose(state);
            True(region.Reclaim().IsEmpty);
        }

        False(state.IsDisposed);

        using (var region = epoch.Enter())
        {
            var bin = region.Reclaim(drainGlobalCache: true);
            False(bin.IsEmpty);
            bin.Clear();
        }
        
        True(state.IsDisposed);
    }

    [Fact]
    public static void AsyncReclamation()
    {
        using var state = new ManualResetEvent(initialState: false);
        var epoch = new Epoch();

        using (var region = epoch.Enter())
        {
            region.Defer(state, static s => s.Set());
            True(region.Reclaim().IsEmpty);
        }

        False(state.WaitOne(0));

        using (var region = epoch.Enter())
        {
            var bin = region.Reclaim(drainGlobalCache: true);
            False(bin.IsEmpty);
            bin.QueueCleanup();
        }
        
        True(state.WaitOne(DefaultTimeout));
    }

    [Fact]
    public static async Task AsyncReclamation2()
    {
        var state = new TaskCompletionSource();
        var epoch = new Epoch();
        using (var region = epoch.Enter())
        {
            region.Defer(new TaskCompletionSourceWrapper(state));
            True(region.Reclaim().IsEmpty);
        }

        False(state.Task.IsCanceled);

        using (var region = epoch.Enter())
        {
            var bin = region.Reclaim(drainGlobalCache: true);
            False(bin.IsEmpty);
            await bin.ClearAsync();
        }

        await state.Task;
    }

    [Fact]
    public static void EmptyScope()
    {
        var scope = default(Epoch.Scope);
        scope.Dispose();
    }

    [Fact]
    public static async Task ClearAsync()
    {
        var epoch = new Epoch();
        Task task;
        using (epoch.Enter())
        {
            task = epoch.ClearAsync(TestToken).AsTask();
            False(task.IsCompleted);
        }

        await task;
    }

    [Theory]
    [InlineData(100, 1, 1, 2)]
    [InlineData(100, 3, 2, 2)]
    public static void StressTest(int depth, int pushCount, int popCount, int readersCount)
    {
        var epoch = new Epoch();
        var stack = new TreiberStack<Int128>(epoch);
        var threads = new List<Thread>(pushCount + popCount + readersCount);
        threads.AddRange(GetThreads(PushThread, pushCount));
        threads.AddRange(GetThreads(PopThread, popCount));
        threads.AddRange(GetThreads(ReadThread, readersCount));

        foreach (var t in threads)
        {
            t.Start();
        }

        foreach (var t in threads)
        {
            t.Join();
        }

        True(epoch.TryClear());

        static Thread[] GetThreads(ThreadStart action, int count)
        {
            var result = new Thread[count];

            for (var i = 0; i < result.Length; i++)
            {
                result[i] = new(action);
            }

            return result;
        }

        void PushThread()
        {
            for (var i = 0; i < depth; i++)
            {
                stack.Push(i);
            }
        }

        void PopThread()
        {
            for (var i = 0; i < depth; i++)
            {
                stack.TryPop(out _);
            }
        }

        void ReadThread()
        {
            for (var i = 0; i < depth; i++)
            {
                stack.TryPeek(out _);
            }
        }
    }
    
    private sealed class TreiberStack<T>(Epoch epoch)
    {
        private volatile Node head;

        public void Push(T value)
        {
            Epoch.RecycleBin bin;
            using (var scope = epoch.Enter())
            {
                Node newNode = new(value), current;

                do
                {
                    newNode.Next = current = head;
                } while (Interlocked.CompareExchange(ref head, newNode, current) != current);

                bin = scope.Reclaim(drainGlobalCache: true);
            }

            bin.Clear();
        }

        public bool TryPop(out T value)
        {
            Epoch.RecycleBin bin;
            using (var scope = epoch.Enter())
            {
                Node currentNode, newNode;
                do
                {
                    currentNode = head;

                    if (currentNode is null)
                    {
                        value = default;
                        return false;
                    }

                    newNode = currentNode.Next;
                } while (Interlocked.CompareExchange(ref head, newNode, currentNode) != currentNode);

                False(currentNode.IsDead);
                value = currentNode.Value;
                scope.Defer(currentNode, static node => node.IsDead = true);

                bin = scope.Reclaim(drainGlobalCache: true);
            }

            bin.Clear();
            return true;
        }

        public bool TryPeek(out T value)
        {
            using var scope = epoch.Enter();
            if (head is { } top)
            {
                False(top.IsDead);
                value = top.Value;
            }

            value = default;
            return false;
        }

        private sealed class Node(T value)
        {
            internal bool IsDead;
            internal Node Next;

            internal T Value => value;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void UnmanagedReclamation(bool throwOnFirstException)
    {
        var manager = new ResourceManager(3);
        var epoch = new Epoch { NativeResourceManager = manager };

        using (var region = epoch.Enter())
        {
            region.Defer(manager.Allocate());
            region.Defer(manager.Allocate());
            region.Defer(manager.Allocate());
            True(region.Reclaim().IsEmpty);
        }

        False(manager.AllReleasedOnce);

        using (var region = epoch.Enter())
        {
            var bin = region.Reclaim(drainGlobalCache: true);
            False(bin.IsEmpty);
            bin.Clear(throwOnFirstException);
        }

        True(manager.AllReleasedOnce);
    }

    [Fact]
    public static void UnmanagedResourcesFromManyThreads()
    {
        const int threadCount = 8, perThread = 1000;
        var manager = new ResourceManager(threadCount * perThread);
        var epoch = new Epoch { NativeResourceManager = manager };

        // each thread defers to its own bucket
        var threads = new Thread[threadCount];
        for (var i = 0; i < threads.Length; i++)
        {
            threads[i] = new(() =>
            {
                for (var j = 0; j < perThread; j++)
                {
                    using var region = epoch.Enter();
                    region.Defer(manager.Allocate());
                }
            });
        }

        foreach (var t in threads)
            t.Start();

        foreach (var t in threads)
            True(t.Join(DefaultTimeout));

        True(epoch.TryClear());
        True(manager.AllReleasedOnce);
    }

    [Fact]
    public static async Task ManagedAndUnmanagedResourcesInDifferentBuckets()
    {
        var manager = new ResourceManager(1);
        var epoch = new Epoch { NativeResourceManager = manager };
        var executed = false;

        var managedThread = new Thread(() =>
        {
            using var region = epoch.Enter();
            region.Defer(() => executed = true);
        });

        var unmanagedThread = new Thread(() =>
        {
            using var region = epoch.Enter();
            region.Defer(manager.Allocate());
        });

        managedThread.Start();
        unmanagedThread.Start();
        True(managedThread.Join(DefaultTimeout));
        True(unmanagedThread.Join(DefaultTimeout));

        await epoch.ClearAsync(TestToken);
        True(executed);
        True(manager.AllReleasedOnce);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void FailedActionDoesNotPreventRelease(bool throwOnFirstException)
    {
        var manager = new ResourceManager(1);
        var epoch = new Epoch { NativeResourceManager = manager };

        using (var region = epoch.Enter())
        {
            region.Defer(static () => throw new ArithmeticException());
            region.Defer(static () => throw new ArithmeticException());
            region.Defer(manager.Allocate());
            True(region.Reclaim().IsEmpty);
        }

        Epoch.RecycleBin bin;
        using (var region = epoch.Enter())
        {
            bin = region.Reclaim(drainGlobalCache: true);
        }

        False(bin.IsEmpty);
        if (throwOnFirstException)
        {
            Throws<ArithmeticException>(() => bin.Clear(throwOnFirstException: true));
        }
        else
        {
            // a single exception is rethrown as is, the multiple ones are aggregated
            Equal(2, Throws<AggregateException>(() => bin.Clear()).InnerExceptions.Count);
        }

        True(manager.AllReleasedOnce);
    }

    [Fact]
    public static void DeferUnmanagedResourceValidation()
    {
        var epoch = new Epoch();
        using (var region = epoch.Enter())
        {
            Throws<InvalidOperationException>(() => region.Defer(1U));
        }

        epoch = new() { NativeResourceManager = new ResourceManager(1) };
        using (var region = epoch.Enter())
        {
            Throws<ArgumentOutOfRangeException>(() => region.Defer(0U));
        }
    }
}

file readonly struct TaskCompletionSourceWrapper(TaskCompletionSource source) : IThreadPoolWorkItem
{
    void IThreadPoolWorkItem.Execute() => source.SetResult();
}
    
file sealed class DisposableObject : Disposable
{
    public new bool IsDisposed => base.IsDisposed;
}
    
file sealed class DiscardableObject : Epoch.Discardable
{
    internal bool IsDisposed { get; private set; }

    protected override void Discard() => IsDisposed = true;
}

// the handle is an index of the resource, the link is stored in the managed array
file sealed class ResourceManager(int capacity) : Epoch.INativeResourceManager
{
    private readonly nuint[] links = new nuint[capacity + 1];
    private readonly int[] releases = new int[capacity + 1];
    private int count;

    public nuint Allocate() => (nuint)Interlocked.Increment(ref count);

    public bool AllReleasedOnce => releases.AsSpan(1, count).ContainsAnyExcept(1) is false;

    public ref nuint GetNextHandle(nuint handle) => ref links[(int)handle];

    public void Release(nuint handle) => Interlocked.Increment(ref releases[(int)handle]);
}