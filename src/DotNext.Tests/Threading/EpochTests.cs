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

        epoch.Enter(drainGlobalCache: false, out Epoch.Scope scope);
        using (scope)
        {
            scope.Defer(() => state.Value = 42);
        }

        Equal(0, state.Value);

        epoch.Enter().Dispose();
        Equal(0, state.Value);

        epoch.Enter(drainGlobalCache: false, out scope);
        scope.Dispose();
        Equal(0, state.Value);

        epoch.Enter(drainGlobalCache: true, out Epoch.RecycleBin action).Dispose();
        False(action.IsEmpty);
        Equal(0, state.Value);

        action.Clear(throwOnFirstException);
        Equal(42, state.Value);
    }

    [Fact]
    public static void Reclamation()
    {
        var state = new DisposableObject();
        var epoch = new Epoch();

        epoch.Enter(drainGlobalCache: false, out Epoch.Scope region);
        try
        {
            region.RegisterForDispose(state);
        }
        finally
        {
            region.Dispose();
        }

        epoch.Enter(drainGlobalCache: false, out region);
        region.Dispose();

        // causes reclamation
        epoch.Enter(drainGlobalCache: false, out region);
        region.Dispose();
        
        True(state.IsDisposed);
    }
    
    [Fact]
    public static void Reclamation2()
    {
        var state = new DiscardableObject();
        var epoch = new Epoch();

        epoch.Enter(drainGlobalCache: false, out Epoch.Scope region);
        try
        {
            region.RegisterForDiscard(state);
        }
        finally
        {
            region.Dispose();
        }

        epoch.Enter(drainGlobalCache: false, out region);
        region.Dispose();

        // causes reclamation
        epoch.Enter(drainGlobalCache: false, out region);
        region.Dispose();
        
        True(state.IsDisposed);
    }

    [Fact]
    public static void AsyncReclamation()
    {
        using var state = new ManualResetEvent(initialState: false);
        var epoch = new Epoch();

        epoch.Enter(drainGlobalCache: false, out Epoch.Scope region);
        using (region)
        {
            region.Defer(state, static state => state.Set());
        }

        epoch.Enter(drainGlobalCache: false, out region);
        region.Dispose();

        // causes reclamation
        epoch.Enter(drainGlobalCache: true, out Epoch.RecycleBin action).Dispose();
        action.QueueCleanup();
        True(state.WaitOne(DefaultTimeout));
    }

    [Fact]
    public static async Task AsyncReclamation2()
    {
        var state = new TaskCompletionSource();

        await ReclaimAsync(state).WaitAsync(TestToken);
        await state.Task.WaitAsync(TestToken);

        static Task ReclaimAsync(TaskCompletionSource source)
        {
            var epoch = new Epoch();

            epoch.Enter(drainGlobalCache: false, out Epoch.Scope region);
            using (region)
            {
                region.Defer(new TaskCompletionSourceWrapper(source));
            }

            epoch.Enter(drainGlobalCache: false, out region);
            region.Dispose();

            // causes reclamation
            epoch.Enter(drainGlobalCache: false, out Epoch.RecycleBin action).Dispose();
            return action.ClearAsync();
        }
    }

    [Fact]
    public static void EmptyScope()
    {
        var scope = default(Epoch.Scope);
        scope.Dispose();
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

        epoch.UnsafeClear();

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
            var scope = default(Epoch.Scope);
            try
            {
                epoch.Enter(drainGlobalCache: true, out scope);
                Node newNode = new(value), current;

                do
                {
                    newNode.Next = current = head;
                } while (Interlocked.CompareExchange(ref head, newNode, current) != current);
            }
            finally
            {
                scope.Dispose();
            }
        }

        public bool TryPop(out T value)
        {
            var scope = default(Epoch.Scope);
            try
            {
                epoch.Enter(drainGlobalCache: true, out scope);
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
            }
            finally
            {
                scope.Dispose();
            }

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
    
    private readonly struct TaskCompletionSourceWrapper(TaskCompletionSource source) : IThreadPoolWorkItem
    {
        void IThreadPoolWorkItem.Execute() => source.SetResult();
    }
    
    private sealed class DisposableObject : Disposable
    {
        public new bool IsDisposed => base.IsDisposed;
    }
    
    private sealed class DiscardableObject : Epoch.Discardable
    {
        internal bool IsDisposed { get; private set; }

        protected override void Discard() => IsDisposed = true;
    }
}