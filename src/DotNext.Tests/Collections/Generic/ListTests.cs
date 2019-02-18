using System.Collections.Generic;
using Xunit;

namespace DotNext.Collections.Generic
{
    public sealed class ListTests: Assert
    {
        [Fact]
        public void ToArrayTest()
        {
            var list = new List<long>() { 10, 40, 100 };
            var array = list.ToArray(i => i.ToString());
            True(array.SequenceEqual(new string[] { "10", "40", "100" }));
        }
    }
}
