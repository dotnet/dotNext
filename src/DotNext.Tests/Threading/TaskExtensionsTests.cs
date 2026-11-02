namespace DotNext.Threading;

public sealed class TaskExtensionsTests : Test
{
    [Fact]
    public static void Spawn1WithoutResult()
    {
        var currentThreadId = Environment.CurrentManagedThreadId;
        var task = Task.Run(Invoke, TestToken);
        SpinWait(task, TestToken);
        
        True(task.IsCompletedSuccessfully);

        Task Invoke(CancellationToken token)
        {
            NotEqual(currentThreadId, Environment.CurrentManagedThreadId);
            return token.IsCancellationRequested
                ? Task.FromCanceled(token)
                : Task.CompletedTask;
        }
    }
    
    [Fact]
    public static void Spawn2WithoutResult()
    {
        var currentThreadId = Environment.CurrentManagedThreadId;
        var task = Task.Run(Invoke, 42, TestToken);
        SpinWait(task, TestToken);
        
        True(task.IsCompletedSuccessfully);

        Task Invoke(int arg, CancellationToken token)
        {
            Equal(42, arg);
            NotEqual(currentThreadId, Environment.CurrentManagedThreadId);
            return token.IsCancellationRequested
                ? Task.FromCanceled(token)
                : Task.CompletedTask;
        }
    }
    
    [Fact]
    public static void Spawn3WithoutResult()
    {
        var currentThreadId = Environment.CurrentManagedThreadId;
        var task = Task.Run(Invoke, 42, "42", TestToken);
        SpinWait(task, TestToken);
        
        True(task.IsCompletedSuccessfully);

        Task Invoke(int arg1, string arg2, CancellationToken token)
        {
            Equal(42, arg1);
            Equal("42", arg2);
            NotEqual(currentThreadId, Environment.CurrentManagedThreadId);
            return token.IsCancellationRequested
                ? Task.FromCanceled(token)
                : Task.CompletedTask;
        }
    }
    
    [Fact]
    public static void Spawn4WithoutResult()
    {
        var currentThreadId = Environment.CurrentManagedThreadId;
        var task = Task.Run(Invoke, 42, "42", true, TestToken);
        SpinWait(task, TestToken);
        
        True(task.IsCompletedSuccessfully);

        Task Invoke(int arg1, string arg2, bool arg3, CancellationToken token)
        {
            Equal(42, arg1);
            Equal("42", arg2);
            True(arg3);
            NotEqual(currentThreadId, Environment.CurrentManagedThreadId);
            return token.IsCancellationRequested
                ? Task.FromCanceled(token)
                : Task.CompletedTask;
        }
    }

    [Fact]
    public static async Task Spawn1()
    {
        var currentThreadId = Environment.CurrentManagedThreadId;
        var task = Task.Run(Invoke, TestToken);
        SpinWait(task, TestToken);
        Equal(42, await task);

        Task<int> Invoke(CancellationToken token)
        {
            NotEqual(currentThreadId, Environment.CurrentManagedThreadId);
            return token.IsCancellationRequested
                ? Task.FromCanceled<int>(token)
                : Task.FromResult(42);
        }
    }
    
    [Fact]
    public static async Task Spawn2()
    {
        var currentThreadId = Environment.CurrentManagedThreadId;
        var task = Task.Run(Invoke, 42, TestToken);
        SpinWait(task, TestToken);
        Equal(42, await task);

        Task<int> Invoke(int arg, CancellationToken token)
        {
            NotEqual(currentThreadId, Environment.CurrentManagedThreadId);
            return token.IsCancellationRequested
                ? Task.FromCanceled<int>(token)
                : Task.FromResult(arg);
        }
    }
    
    [Fact]
    public static async Task Spawn3()
    {
        var currentThreadId = Environment.CurrentManagedThreadId;
        var task = Task.Run(Invoke, 40, 2, TestToken);
        SpinWait(task, TestToken);
        Equal(42, await task);

        Task<int> Invoke(int arg1, int arg2, CancellationToken token)
        {
            NotEqual(currentThreadId, Environment.CurrentManagedThreadId);
            return token.IsCancellationRequested
                ? Task.FromCanceled<int>(token)
                : Task.FromResult(arg1 + arg2);
        }
    }
    
    [Fact]
    public static async Task Spawn4()
    {
        var currentThreadId = Environment.CurrentManagedThreadId;
        var task = Task.Run(Invoke, 39, 2, 1, TestToken);
        SpinWait(task, TestToken);
        Equal(42, await task);

        Task<int> Invoke(int arg1, int arg2, int arg3, CancellationToken token)
        {
            NotEqual(currentThreadId, Environment.CurrentManagedThreadId);
            return token.IsCancellationRequested
                ? Task.FromCanceled<int>(token)
                : Task.FromResult(arg1 + arg2 + arg3);
        }
    }
}