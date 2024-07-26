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
        }

        Equal(0, state.Value);

        epoch.Enter().Dispose();
        Equal(0, state.Value);

        epoch.Enter(asyncReclamation: null).Dispose();
        Equal(0, state.Value);

        epoch.Enter(out var action).Dispose();
        False(action.IsEmpty);
        Equal(0, state.Value);

        action.Invoke(throwOnFirstException);
        Equal(42, state.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void ReclamationActionAsDelegate(bool throwOnFirstException)
    {
        var state = new StrongBox<int>();
        var epoch = new Epoch();

        using (var region = epoch.Enter())
        {
            region.Defer(new StrongBoxWrapper<int>(state, 42));
        }

        Equal(0, state.Value);

        epoch.Enter().Dispose();
        Equal(0, state.Value);

        epoch.Enter(asyncReclamation: null).Dispose();
        Equal(0, state.Value);

        epoch.Enter(out var action).Dispose();
        False(action.IsEmpty);
        Equal(0, state.Value);

        action.ToDelegate(throwOnFirstException).Invoke();
        Equal(42, state.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void Reclamation(bool throwOnFirstException)
    {
        var state = new DisposableObject();
        var epoch = new Epoch();

        using (var region = epoch.Enter())
        {
            region.Defer(state);
        }

        epoch.Enter().Dispose();

        epoch.Enter(throwOnFirstException).Dispose(); // causes reclamation
        True(state.IsDisposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void AsyncReclamation(bool throwOnFirstException)
    {
        using var state = new ManualResetEvent(initialState: false);
        var epoch = new Epoch();

        using (var region = epoch.Enter())
        {
            region.Defer(state, static state => state.Set());
        }

        epoch.Enter().Dispose();

        epoch.Enter(out var action).Dispose(); // causes reclamation
        action.Start(throwOnFirstException);
        True(state.WaitOne(DefaultTimeout));
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

        epoch.UnsafeReclaim();

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
            using var scope = epoch.Enter();
            Node newNode = new(value), current;

            do
            {
                newNode.Next = current = head;
            } while (Interlocked.CompareExchange(ref head, newNode, current) != current);
        }

        public bool TryPop(out T value)
        {
            using var scope = epoch.Enter();
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
    
    private struct StrongBoxWrapper<T>(StrongBox<T> box, T newValue) : IThreadPoolWorkItem
    {
        void IThreadPoolWorkItem.Execute() => box.Value = newValue;
    }
    
    private sealed class DisposableObject : Disposable
    {
        public new bool IsDisposed => base.IsDisposed;
    }
}