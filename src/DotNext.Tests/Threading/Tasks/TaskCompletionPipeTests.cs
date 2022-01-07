using System.Diagnostics.CodeAnalysis;

namespace DotNext.Threading.Tasks
{
    [ExcludeFromCodeCoverage]
    public class TaskCompletionPipeTests : Test
    {
        [Fact]
        public static async Task StressTest()
        {
            var pipe = new TaskCompletionPipe<Task<int>>(4);
            for (var i = 0; i < 100; i++)
            {
                pipe.Add(Task.Run<int>(async () =>
                {
                    await Task.Delay(Random.Shared.Next(10, 100));
                    return 42;
                }));
            }

            pipe.Complete();

            var result = 0;
            await foreach (var task in pipe)
            {
                result += task.Result;
            }

            Equal(4200, result);
        }

        [Fact]
        public static async Task StressTest2()
        {
            var pipe = new TaskCompletionPipe<Task<int>>(4);
            for (var i = 0; i < 100; i++)
            {
                pipe.Add(Task.Run<int>(async () =>
                {
                    await Task.Delay(Random.Shared.Next(10, 100));
                    return 42;
                }));
            }

            pipe.Complete();

            var result = 0;
            while (await pipe.WaitToReadAsync())
            {
                if (pipe.TryRead(out var task))
                    result += task.Result;
            }

            Equal(4200, result);
        }

        [Fact]
        public static async Task QueueGrowth()
        {
            var pipe = new TaskCompletionPipe<Task<int>>(4);
            pipe.Add(Task.FromResult(42));
            pipe.Add(Task.FromResult(43));
            True(await pipe.WaitToReadAsync());
            True(pipe.TryRead(out var task));
            Equal(42, task.Result);

            pipe.Add(Task.FromResult(44));
            pipe.Add(Task.FromResult(45));
            pipe.Add(Task.FromResult(46));

            await using (var enumerator = pipe.GetAsyncEnumerator(CancellationToken.None))
            {
                True(await enumerator.MoveNextAsync());
                Equal(43, enumerator.Current.Result);

                True(await enumerator.MoveNextAsync());
                Equal(44, enumerator.Current.Result);

                True(await enumerator.MoveNextAsync());
                Equal(45, enumerator.Current.Result);

                True(await enumerator.MoveNextAsync());
                Equal(46, enumerator.Current.Result);
            }

            False(pipe.TryRead(out task));
        }

        [Fact]
        public static async Task ResetWhenScheduled()
        {
            var pipe = new TaskCompletionPipe<Task>(capacity: 1);
            var source = new TaskCompletionSource();
            pipe.Add(source.Task);

            pipe.Reset();
            pipe.Complete();
            source.SetResult();
            False(await pipe.WaitToReadAsync());
        }
    }
}