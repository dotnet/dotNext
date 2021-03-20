using System.Threading.Tasks;
using Xunit;

namespace DotNext.IO.Log
{
    public sealed class LogEntryProducerTests : Test
    {
        [Fact]
        public static async Task EmptyProducer()
        {
            await using ILogEntryProducer<ILogEntry> producer = new LogEntryProducer<ILogEntry>();
            Equal(0L, producer.RemainingCount);
        }
    }
}