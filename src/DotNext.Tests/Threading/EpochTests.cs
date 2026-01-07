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