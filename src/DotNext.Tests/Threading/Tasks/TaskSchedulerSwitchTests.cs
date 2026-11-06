namespace DotNext.Threading.Tasks;

public sealed class TaskSchedulerSwitchTests : Test
{
    [Fact]
    public static Task NoYieldOnThreadPool() => Task.Run(static () =>
    {
        True(TaskScheduler.Default.SwitchTo().GetAwaiter().IsCompleted);
        False(TaskScheduler.Default.SwitchTo(forceYield: true).GetAwaiter().IsCompleted);
    }, TestToken);

    [Fact]
    public static Task AlwaysYield() => Task.Run(static async () =>
    {
        await TaskScheduler.Default.SwitchTo(forceYield: true);

        True(Thread.CurrentThread.IsThreadPoolThread);
        Same(TaskScheduler.Default, TaskScheduler.Current);
    }, TestToken);

    [Fact]
    public static Task YieldWithSynchronizationContext() => Task.Run(static async () =>
    {
        // the builder restores the synchronization context of the thread when the method yields
        SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());
        False(TaskScheduler.Default.SwitchTo().GetAwaiter().IsCompleted);

        await TaskScheduler.Default.SwitchTo();
        Null(SynchronizationContext.Current);
        True(Thread.CurrentThread.IsThreadPoolThread);
    }, TestToken);

    [Fact]
    public static async Task SwitchToCustomScheduler()
    {
        var pair = new ConcurrentExclusiveSchedulerPair();
        await Task.Run(async () =>
        {
            await pair.ExclusiveScheduler.SwitchTo();
            Same(pair.ExclusiveScheduler, TaskScheduler.Current);
            True(pair.ExclusiveScheduler.SwitchTo().GetAwaiter().IsCompleted);

            // subsequent continuations are executed by the same scheduler
            await Task.Delay(10, TestToken);
            Same(pair.ExclusiveScheduler, TaskScheduler.Current);

            await TaskScheduler.Default.SwitchTo();
            Same(TaskScheduler.Default, TaskScheduler.Current);
            True(Thread.CurrentThread.IsThreadPoolThread);
        }, TestToken);

        pair.Complete();
        await pair.Completion.WaitAsync(DefaultTimeout, TestToken);
    }

    [Fact]
    public static async Task FlowExecutionContext()
    {
        var pair = new ConcurrentExclusiveSchedulerPair();
        var local = new AsyncLocal<int>();
        await Task.Run(async () =>
        {
            local.Value = 42;

            await pair.ExclusiveScheduler.SwitchTo();
            Equal(42, local.Value);

            await TaskScheduler.Default.SwitchTo(forceYield: true);
            Equal(42, local.Value);
        }, TestToken);
    }

    [Fact]
    public static async Task FlowExecutionContextWithoutBuilder()
    {
        var pair = new ConcurrentExclusiveSchedulerPair();
        var local = new AsyncLocal<int> { Value = 42 };
        var result1 = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var result2 = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        // OnCompleted must flow the execution context, in contrast to UnsafeOnCompleted
        pair.ExclusiveScheduler.SwitchTo().GetAwaiter().OnCompleted(() => result1.SetResult(local.Value));
        TaskScheduler.Default.SwitchTo(forceYield: true).GetAwaiter().OnCompleted(() => result2.SetResult(local.Value));

        Equal(42, await result1.Task.WaitAsync(DefaultTimeout, TestToken));
        Equal(42, await result2.Task.WaitAsync(DefaultTimeout, TestToken));
    }

    [Fact]
    public static void NullScheduler()
    {
        Throws<ArgumentNullException>(static () => default(TaskScheduler)!.SwitchTo());
    }

    [Fact]
    public static async Task ScopeRestoresScheduler()
    {
        var pair = new ConcurrentExclusiveSchedulerPair();
        await Task.Run(async () =>
        {
            await using (await pair.ExclusiveScheduler.SwitchToAsync())
            {
                Same(pair.ExclusiveScheduler, TaskScheduler.Current);
                await Task.Delay(10, TestToken);
                Same(pair.ExclusiveScheduler, TaskScheduler.Current);
            }

            Same(TaskScheduler.Default, TaskScheduler.Current);
            True(Thread.CurrentThread.IsThreadPoolThread);
        }, TestToken);
    }

    [Fact]
    public static async Task ScopeRestoresSchedulerOnException()
    {
        var pair = new ConcurrentExclusiveSchedulerPair();
        await Task.Run(async () =>
        {
            try
            {
                await using (await pair.ExclusiveScheduler.SwitchToAsync())
                {
                    Same(pair.ExclusiveScheduler, TaskScheduler.Current);
                    throw new ArithmeticException();
                }
            }
            catch (ArithmeticException)
            {
                Same(TaskScheduler.Default, TaskScheduler.Current);
            }
        }, TestToken);
    }

    [Fact]
    public static async Task NestedScopes()
    {
        var pair1 = new ConcurrentExclusiveSchedulerPair();
        var pair2 = new ConcurrentExclusiveSchedulerPair();
        await Task.Run(async () =>
        {
            await using (await pair1.ExclusiveScheduler.SwitchToAsync())
            {
                await using (await pair2.ExclusiveScheduler.SwitchToAsync())
                {
                    Same(pair2.ExclusiveScheduler, TaskScheduler.Current);
                }

                Same(pair1.ExclusiveScheduler, TaskScheduler.Current);

                // already on the target scheduler, so neither the scope nor its disposal switches
                var awaiter = pair1.ExclusiveScheduler.SwitchToAsync().GetAwaiter();
                True(awaiter.IsCompleted);
                await using (awaiter.GetResult())
                {
                    Same(pair1.ExclusiveScheduler, TaskScheduler.Current);
                }

                Same(pair1.ExclusiveScheduler, TaskScheduler.Current);
            }

            Same(TaskScheduler.Default, TaskScheduler.Current);
        }, TestToken);
    }

    [Fact]
    public static async Task ScopeRestoresSynchronizationContext()
    {
        var pair = new ConcurrentExclusiveSchedulerPair();
        var context = new ThreadPoolSynchronizationContext();
        await Task.Run(async () =>
        {
            SynchronizationContext.SetSynchronizationContext(context);

            await using (await pair.ExclusiveScheduler.SwitchToAsync())
            {
                Same(pair.ExclusiveScheduler, TaskScheduler.Current);
                Null(SynchronizationContext.Current);
            }

            Same(context, SynchronizationContext.Current);

            // the continuation is posted directly to the context, as await does
            Same(TaskScheduler.Default, TaskScheduler.Current);
            Null(Task.CurrentId);
        }, TestToken);
    }

    [Fact]
    public static async Task ScopeRestoresSynchronizationContextWithoutBuilder()
    {
        var pair = new ConcurrentExclusiveSchedulerPair();
        var context = new ThreadPoolSynchronizationContext();
        var local = new AsyncLocal<int> { Value = 42 };
        var result = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        var awaiter = pair.ExclusiveScheduler.SwitchToAsync().GetAwaiter();
        True(awaiter.GetResult().DisposeAsync().GetAwaiter().IsCompleted); // still in the captured context
        SynchronizationContext.SetSynchronizationContext(previous);

        var disposeAwaiter = awaiter.GetResult().DisposeAsync().GetAwaiter();
        False(disposeAwaiter.IsCompleted);

        // OnCompleted must flow the execution context
        disposeAwaiter.OnCompleted(() => result.SetResult(SynchronizationContext.Current == context ? local.Value : -1));
        Equal(42, await result.Task.WaitAsync(DefaultTimeout, TestToken));
    }

    [Fact]
    public static void NullSchedulerScope()
    {
        Throws<ArgumentNullException>(static () => default(TaskScheduler)!.SwitchToAsync());
    }
}

file sealed class ThreadPoolSynchronizationContext : SynchronizationContext
{
    public override void Post(SendOrPostCallback d, object state)
        => ThreadPool.QueueUserWorkItem(_ =>
        {
            SetSynchronizationContext(this);
            try
            {
                d(state);
            }
            finally
            {
                SetSynchronizationContext(null);
            }
        });
}