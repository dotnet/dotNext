using System;
using Xunit;

namespace DotNext
{
    public sealed class DelegateHelpersTests : Assert
    {
        [Fact]
        public static void ContravarianceTest()
        {
            EventHandler<string> handler = null;
            EventHandler<object> dummy = (sender, args) => { };
            handler += dummy.Contravariant<object, string>();
            NotNull(handler);
            handler -= dummy.Contravariant<object, string>();
            Null(handler);
        }

        [Fact]
        public static void OpenDelegate()
        {
            var d = DelegateHelpers.CreateOpenDelegate<Func<string, char, int, int>>((str, ch, startIndex) => str.IndexOf(ch, startIndex));
            NotNull(d);
            Equal(1, d("abc", 'b', 0));
        }
    }
}