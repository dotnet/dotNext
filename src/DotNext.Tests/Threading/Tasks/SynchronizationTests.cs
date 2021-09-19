using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace DotNext.Threading.Tasks
{
    [ExcludeFromCodeCoverage]
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
            Throws<AggregateException>(() => result.Value);
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
            IsType<AggregateException>(result.Error);
            task = Task.FromException(new InvalidOperationException());
            result = task.GetResult(TimeSpan.Zero);
            IsType<AggregateException>(result.Error);
        }
    }
}