using System;
using Xunit;

namespace DotNext
{
    public sealed class DelegatesTest: Assert
    {
        [Fact]
        public void ContravarianceTest()
        {
            EventHandler<string> handler = null;
            EventHandler<object> dummy = (sender, args) => { };
            handler += dummy.Contravariant<object, string>();
            NotNull(handler);
            handler -= dummy.Contravariant<object, string>();
            Null(handler);
        }
    }
}