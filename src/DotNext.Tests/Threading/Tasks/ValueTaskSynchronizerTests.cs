namespace DotNext.Threading.Tasks;

public sealed class ValueTaskSynchronizerTests : Test
{
    private sealed class SharedCounter
    {
        private long value = 0;

        internal void Inc() => Interlocked.Increment(ref value);

        internal long Value => value;
    }

    [Fact]
    public static async Task WhenAll2()
    {
        var counter = new SharedCounter();
        var source1 = new TaskCompletionSource();
        var source2 = new TaskCompletionSource();

        ThreadPool.QueueUserWorkItem(state =>
        {
            counter.Inc();
            Thread.Sleep(100);
            source1.SetResult();
        });
        ThreadPool.QueueUserWorkItem(state =>
        {
            counter.Inc();
            Thread.Sleep(200);
            source2.SetResult();
        });

        await Synchronization.WhenAll(new ValueTask(source1.Task), new ValueTask(source2.Task));
        Equal(2, counter.Value);
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

        var (result1, result2) = await Synchronization.WhenAll(new ValueTask<int>(source1.Task), new ValueTask<int>(source2.Task));

        Equal(10, result1.Value);
        Equal(20, result2.Value);
    }

    [Fact]
    public static async Task WhenAll3()
    {
        var counter = new SharedCounter();
        var source1 = new TaskCompletionSource();
        var source2 = new TaskCompletionSource();
        var source3 = new TaskCompletionSource();

        ThreadPool.QueueUserWorkItem(state =>
        {
            counter.Inc();
            Thread.Sleep(100);
            source1.SetResult();
        });
        ThreadPool.QueueUserWorkItem(state =>
        {
            counter.Inc();
            Thread.Sleep(200);
            source2.SetResult();
        });
        ThreadPool.QueueUserWorkItem(state =>
        {
            counter.Inc();
            Thread.Sleep(150);
            source3.SetResult();
        });

        await Synchronization.WhenAll(new ValueTask(source1.Task), new ValueTask(source2.Task), new ValueTask(source3.Task));
        Equal(3, counter.Value);
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

        var (result1, result2, result3) = await Synchronization.WhenAll(new ValueTask<int>(source1.Task), new ValueTask<int>(source2.Task), new ValueTask<int>(source3.Task));

        Equal(10, result1.Value);
        Equal(20, result2.Value);
        Equal(30, result3.Value);
    }

    [Fact]
    public static async Task WhenAll4()
    {
        var counter = new SharedCounter();
        var source1 = new TaskCompletionSource();
        var source2 = new TaskCompletionSource();
        var source3 = new TaskCompletionSource();
        var source4 = new TaskCompletionSource();

        ThreadPool.QueueUserWorkItem(state =>
        {
            counter.Inc();
            Thread.Sleep(100);
            source1.SetResult();
        });
        ThreadPool.QueueUserWorkItem(state =>
        {
            counter.Inc();
            Thread.Sleep(200);
            source2.SetResult();
        });
        ThreadPool.QueueUserWorkItem(state =>
        {
            counter.Inc();
            Thread.Sleep(150);
            source3.SetResult();
        });

        ThreadPool.QueueUserWorkItem(state =>
        {
            counter.Inc();
            Thread.Sleep(110);
            source4.SetResult();
        });

        await Synchronization.WhenAll(new ValueTask(source1.Task), new ValueTask(source2.Task), new ValueTask(source3.Task), new ValueTask(source4.Task));
        Equal(4, counter.Value);
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

        var (result1, result2, result3, result4) = await Synchronization.WhenAll(new ValueTask<int>(source1.Task), new ValueTask<int>(source2.Task), new ValueTask<int>(source3.Task), new ValueTask<int>(source4.Task));

        Equal(10, result1.Value);
        Equal(20, result2.Value);
        Equal(30, result3.Value);
        Equal(40, result4.Value);
    }

    [Fact]
    public static async Task WhenAll5()
    {
        var counter = new SharedCounter();
        var source1 = new TaskCompletionSource();
        var source2 = new TaskCompletionSource();
        var source3 = new TaskCompletionSource();
        var source4 = new TaskCompletionSource();
        var source5 = new TaskCompletionSource();

        ThreadPool.QueueUserWorkItem(state =>
        {
            counter.Inc();
            Thread.Sleep(100);
            source1.SetResult();
        });
        ThreadPool.QueueUserWorkItem(state =>
        {
            counter.Inc();
            Thread.Sleep(200);
            source2.SetResult();
        });
        ThreadPool.QueueUserWorkItem(state =>
        {
            counter.Inc();
            Thread.Sleep(150);
            source3.SetResult();
        });

        ThreadPool.QueueUserWorkItem(state =>
        {
            counter.Inc();
            Thread.Sleep(110);
            source4.SetResult();
        });

        ThreadPool.QueueUserWorkItem(state =>
        {
            counter.Inc();
            Thread.Sleep(90);
            source5.SetResult();
        });

        await Synchronization.WhenAll(new ValueTask(source1.Task), new ValueTask(source2.Task), new ValueTask(source3.Task), new ValueTask(source4.Task), new ValueTask(source5.Task));
        Equal(5, counter.Value);
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

        var (result1, result2, result3, result4, result5) = await Synchronization.WhenAll(new ValueTask<int>(source1.Task), new ValueTask<int>(source2.Task), new ValueTask<int>(source3.Task), new ValueTask<int>(source4.Task), new ValueTask<int>(source5.Task));

        Equal(10, result1.Value);
        Equal(20, result2.Value);
        Equal(30, result3.Value);
        Equal(40, result4.Value);
        Equal(50, result5.Value);
    }
}