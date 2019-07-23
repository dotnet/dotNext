using System;
using Xunit;

namespace DotNext.Threading
{
    public sealed class VolatileContainerTests : Assert
    {
        [Fact]
        public static void VolatileRead()
        {
            var container = new VolatileContainer<Guid>();
            var value = Guid.NewGuid();
            container.Value = value;
            Equal(value, container.Value);
        }
    }
}