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

        [Fact]
        public static void OpenDelegateForProperty()
        {
            var d = DelegateHelpers.CreateOpenDelegate<Func<string, int>>(str => str.Length);
            NotNull(d);
            Equal(4, d("abcd"));
        }

        [Fact]
        public static void ClosedDelegate()
        {
            var d = DelegateHelpers.CreateClosedDelegateFactory<Func<char, int, int>>((ch, startIndex) => "".IndexOf(ch, startIndex)).Invoke("abc");
            Equal(1, d('b', 0));
        }

        [Fact]
        public static void ClosedDelegateForProperty()
        {
            var d = DelegateHelpers.CreateClosedDelegateFactory<Func<int>>(() => "".Length).Invoke("abcd");
            Equal(4, d());
        }

        [Fact]
        public static void OpenDelegateConversion()
        {
            var d = DelegateHelpers.CreateOpenDelegate<Func<decimal, long>>(i => (long)i);
            Equal(42L, d(42M));
        }
    }
}