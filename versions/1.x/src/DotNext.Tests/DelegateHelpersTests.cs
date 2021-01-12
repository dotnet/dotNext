using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Xunit;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
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
        public static void ChangeDelegateType()
        {
            WaitCallback callback = obj => { };
            callback += obj => { };
            var result = callback.ChangeType<SendOrPostCallback>();
            NotNull(result);
            var list1 = callback.GetInvocationList().Select(d => d.Method);
            var list2 = result.GetInvocationList().Select(d => d.Method);
            Equal(list1, list2);
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

        private static int GetLength(string value) => value.Length;

        [Fact]
        public static void BindUnbind()
        {
            var func = new Func<string, int>(GetLength).Bind("abc");
            Equal(3, func());
            Equal(4, func.Unbind<string, int>().Invoke("abcd"));
        }

        [Fact]
        public static void TryInvoke()
        {
            Func<string, int> parser = int.Parse;
            var result = parser.TryInvoke("123");
            True(result.IsSuccessful);
            Null(result.Error);
            Equal(123, result.Value);

            result = parser.TryInvoke("abc");
            False(result.IsSuccessful);
            NotNull(result.Error);
            Throws<FormatException>(() => result.Value);
        }
    }
}