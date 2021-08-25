using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.IO.Log
{
    [ExcludeFromCodeCoverage]
    public sealed class LogEntryProducerTests : Test
    {
        [Fact]
        public static async Task EmptyProducer()
        {
            await using ILogEntryProducer<ILogEntry> producer = new LogEntryProducer<ILogEntry>();
            False(await producer.MoveNextAsync());
            Equal(0L, producer.RemainingCount);
        }
    }
}