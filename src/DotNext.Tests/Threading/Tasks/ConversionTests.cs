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
        Equal(Missing.Value, result);
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
}