using System.Reflection;

namespace DotNext.Threading.Tasks;

public sealed class SynchronizationTests : Test
{
    [Fact]
    public static void GetResult()
    {
        var task = Task.FromResult(42);
        var result = task.GetResult(TimeSpan.Zero);
        Null(result.Error);
        True(result.IsSuccessful);
        Equal(42, result.Value);

        result = task.GetResult(CancellationToken.None);
        Null(result.Error);
        True(result.IsSuccessful);
        Equal(42, result.Value);

        task = Task.FromCanceled<int>(new CancellationToken(true));
        result = task.GetResult(TimeSpan.Zero);
        NotNull(result.Error);
        False(result.IsSuccessful);
        Throws<TaskCanceledException>(() => result.Value);
    }

    [Fact]
    public static void GetDynamicResult()
    {
        Task task = Task.FromResult(42);
        Result<dynamic> result = task.GetResult(CancellationToken.None);
        Equal(42, result);
        result = task.GetResult(TimeSpan.Zero);
        Equal(42, result);
        task = Task.CompletedTask;
        result = task.GetResult(CancellationToken.None);
        Equal(Missing.Value, result);
        result = task.GetResult(TimeSpan.Zero);
        Equal(Missing.Value, result);
        task = Task.FromCanceled(new CancellationToken(true));
        result = task.GetResult(CancellationToken.None);
        IsType<TaskCanceledException>(result.Error);
        task = Task.FromException(new InvalidOperationException());
        result = task.GetResult(TimeSpan.Zero);
        IsType<InvalidOperationException>(result.Error);
    }

    [Fact]
    public static async Task WhenAllWithResult2()
    {
        var source1 = new TaskCompletionSource<int>();
        var source2 = new TaskCompletionSource<int>();

        ThreadPool.QueueUserWorkItem(state =>
        {
            Thread.Sleep(100);
            source1.SetResult(10);
        });
        ThreadPool.QueueUserWorkItem(state =>
        {
            Thread.Sleep(200);
            source2.SetResult(20);
        });

        var (result1, result2) = await Synchronization.WhenAll(source1.Task, source2.Task);

        Equal(10, result1.Value);
        Equal(20, result2.Value);
    }

    [Fact]
    public static async Task WhenAllWithResult3()
    {
        var source1 = new TaskCompletionSource<int>();
        var source2 = new TaskCompletionSource<int>();
        var source3 = new TaskCompletionSource<int>();

        ThreadPool.QueueUserWorkItem(state =>
        {
            Thread.Sleep(100);
            source1.SetResult(10);
        });
        ThreadPool.QueueUserWorkItem(state =>
        {
            Thread.Sleep(200);
            source2.SetResult(20);
        });
        ThreadPool.QueueUserWorkItem(state =>
        {
            Thread.Sleep(170);
            source3.SetResult(30);
        });

        var (result1, result2, result3) = await Synchronization.WhenAll(source1.Task, source2.Task, source3.Task);

        Equal(10, result1.Value);
        Equal(20, result2.Value);
        Equal(30, result3.Value);
    }

    [Fact]
    public static async Task WhenAllWithResult4()
    {
        var source1 = new TaskCompletionSource<int>();
        var source2 = new TaskCompletionSource<int>();
        var source3 = new TaskCompletionSource<int>();
        var source4 = new TaskCompletionSource<int>();

        ThreadPool.QueueUserWorkItem(state =>
        {
            Thread.Sleep(100);
            source1.SetResult(10);
        });
        ThreadPool.QueueUserWorkItem(state =>
        {
            Thread.Sleep(200);
            source2.SetResult(20);
        });
        ThreadPool.QueueUserWorkItem(state =>
        {
            Thread.Sleep(170);
            source3.SetResult(30);
        });
        ThreadPool.QueueUserWorkItem(state =>
        {
            Thread.Sleep(120);
            source4.SetResult(40);
        });

        var (result1, result2, result3, result4) = await Synchronization.WhenAll(source1.Task, source2.Task, source3.Task, source4.Task);

        Equal(10, result1.Value);
        Equal(20, result2.Value);
        Equal(30, result3.Value);
        Equal(40, result4.Value);
    }

    [Fact]
    public static async Task WhenAllWithResult5()
    {
        var source1 = new TaskCompletionSource<int>();
        var source2 = new TaskCompletionSource<int>();
        var source3 = new TaskCompletionSource<int>();
        var source4 = new TaskCompletionSource<int>();
        var source5 = new TaskCompletionSource<int>();

        ThreadPool.QueueUserWorkItem(state =>
        {
            Thread.Sleep(100);
            source1.SetResult(10);
        });
        ThreadPool.QueueUserWorkItem(state =>
        {
            Thread.Sleep(200);
            source2.SetResult(20);
        });
        ThreadPool.QueueUserWorkItem(state =>
        {
            Thread.Sleep(170);
            source3.SetResult(30);
        });
        ThreadPool.QueueUserWorkItem(state =>
        {
            Thread.Sleep(120);
            source4.SetResult(40);
        });
        ThreadPool.QueueUserWorkItem(state =>
        {
            Thread.Sleep(180);
            source5.SetResult(50);
        });

        var (result1, result2, result3, result4, result5) = await Synchronization.WhenAll(source1.Task, source2.Task, source3.Task, source4.Task, source5.Task);

        Equal(10, result1.Value);
        Equal(20, result2.Value);
        Equal(30, result3.Value);
        Equal(40, result4.Value);
        Equal(50, result5.Value);
    }

    [Fact]
    public static void SynchronousWait()
    {
        var cts = new TaskCompletionSource();
        var thread = new Thread(() =>
        {
            var task = new ValueTask(cts.Task);
            task.Wait();
            
            // ensure that the current thread in not interrupted
            Thread.Sleep(0);
        });
        
        thread.Start();
        Thread.Sleep(100);

        cts.TrySetResult();
        thread.Join();
    }

    [Fact]
    public static void SynchronousWaitWithResult()
    {
        const int expected = 42;
        var cts = new TaskCompletionSource<int>();
        var thread = new Thread(() =>
        {
            var task = new ValueTask<int>(cts.Task);
            Equal(expected, task.Wait());

            // ensure that the current thread in not interrupted
            Thread.Sleep(0);
        });

        thread.Start();
        Thread.Sleep(100);

        cts.TrySetResult(42);
        thread.Join();
    }
}