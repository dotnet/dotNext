using System.Reflection;

namespace DotNext.Threading.Tasks;

public sealed class ConversionTests : Test
{
    [Fact]
    public static async Task Nullable()
    {
        var t = Task.FromResult(10).ToNullable();
        Equal(10, await t);
    }

    [Fact]
    public static async Task TypeConversion()
    {
        var t = Task.FromResult("12").Convert(int.Parse);
        Equal(12, await t);

        t = Task.FromResult("12").Convert(static str => Task.FromResult(int.Parse(str)));
        Equal(12, await t);
    }

    [Fact]
    public static async Task DynamicTask()
    {
        object result = await Task.CompletedTask.AsDynamic();
        Equal(Missing.Value, result);
        result = await Task.FromResult("Hello").AsDynamic();
        Equal("Hello", result);
        //check for caching
        result = await Task.CompletedTask.AsDynamic();
        Same(Missing.Value, result);
        result = await Task.FromResult("Hello2").AsDynamic();
        Equal("Hello2", result);
        await ThrowsAnyAsync<OperationCanceledException>(async () => await Task.FromCanceled(new CancellationToken(true)).AsDynamic());
        await ThrowsAsync<InvalidOperationException>(async () => await Task.FromException(new InvalidOperationException()).AsDynamic());
    }

    [Fact]
    public static async Task DynamicTaskValueType()
    {
        int result = await Task.FromResult(42).AsDynamic();
        Equal(42, result);
    }

    [Fact]
    public static async Task SuspendException()
    {
        await Task.FromException(new Exception()).SuspendException().ConfigureAwait(true);
        await ValueTask.FromException(new Exception()).SuspendException().ConfigureAwait(true);

        var result = await Task.FromException<int>(new ArithmeticException()).SuspendException().ConfigureAwait(true);
        False(result.IsSuccessful);
        IsType<ArithmeticException>(result.Error);

        result = await ValueTask.FromException<int>(new ArithmeticException()).SuspendException().ConfigureAwait(true);
        False(result.IsSuccessful);
        IsType<ArithmeticException>(result.Error);
    }

    [Fact]
    public static async Task SuspendException2()
    {
        var t = Task.FromException(new Exception());
        var result = await t.AsDynamic().ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
        Same(result, Missing.Value);
    }

    [Fact]
    public static async Task ConvertExceptionToError()
    {
        var result = await Task.FromException<int>(new Exception()).SuspendException(ToError).ConfigureAwait(true);
        False(result.IsSuccessful);
        Equal(EnvironmentVariableTarget.Machine, result.Error);
        
        result = await ValueTask.FromException<int>(new Exception()).SuspendException(ToError).ConfigureAwait(true);
        False(result.IsSuccessful);
        Equal(EnvironmentVariableTarget.Machine, result.Error);

        static EnvironmentVariableTarget ToError(Exception e) => EnvironmentVariableTarget.Machine;
    }
    
    [Fact]
    public static async Task SuspendExceptionParametrized()
    {
        await Task.FromException(new Exception()).SuspendException(42, (_, i) => i is 42).ConfigureAwait(true);
        await ValueTask.FromException(new Exception()).SuspendException(42, (_, i) => i is 42).ConfigureAwait(true);
        await ThrowsAsync<Exception>(async () => await Task.FromException(new Exception()).SuspendException(43, (_, i) => i is 42).ConfigureAwait(true));
    }
}