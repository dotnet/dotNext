using System.Collections.Generic;
using Xunit;

namespace DotNext.Collections.Generic
{
    public sealed class ListTests: Assert
    {
        [Fact]
        public static void ToArray()
        {
            var list = new List<long>() { 10, 40, 100 };
            var array = list.ToArray(i => i.ToString());
            True(array.SequenceEqual(new[] { "10", "40", "100" }));
        }
    }
}
