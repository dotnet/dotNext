using System.Reflection;

namespace DotNext.Threading;

public sealed class ThreadPoolWorkItemFactoryTests : Test
{

    private static unsafe IThreadPoolWorkItem CreateWorkItem(TaskCompletionSource<int> source)
    {
        return ThreadPoolWorkItemFactory.Create(&Complete, source);

        static void Complete(TaskCompletionSource<int> source) => source.SetResult(42);
    }

    private static unsafe IThreadPoolWorkItem CreateWorkItem(TaskCompletionSource<int> source, Missing arg2)
    {
        return ThreadPoolWorkItemFactory.Create(&Complete, source, arg2);

        static void Complete(TaskCompletionSource<int> source, Missing arg2) => source.SetResult(42);
    }

    private static unsafe IThreadPoolWorkItem CreateWorkItem(TaskCompletionSource<int> source, Missing arg2, Missing arg3)
    {
        return ThreadPoolWorkItemFactory.Create(&Complete, source, arg2, arg3);

        static void Complete(TaskCompletionSource<int> source, Missing arg2, Missing arg3) => source.SetResult(42);
    }

    [Fact]
    public static async Task WorkItemWithSingleArg()
    {
        var source = new TaskCompletionSource<int>();
        ThreadPool.UnsafeQueueUserWorkItem(CreateWorkItem(source), false);
        Equal(42, await source.Task);
    }

    [Fact]
    public static async Task WorkItemWithTwoArgs()
    {
        var source = new TaskCompletionSource<int>();
        ThreadPool.UnsafeQueueUserWorkItem(CreateWorkItem(source, Missing.Value), false);
        Equal(42, await source.Task);
    }

    [Fact]
    public static async Task WorkItemWithThreeArgs()
    {
        var source = new TaskCompletionSource<int>();
        ThreadPool.UnsafeQueueUserWorkItem(CreateWorkItem(source, Missing.Value, Missing.Value), false);
        Equal(42, await source.Task);
    }
}