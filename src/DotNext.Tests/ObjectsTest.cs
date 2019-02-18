using System;
using Xunit;

namespace DotNext
{
    public sealed class ObjectsTest: Assert
    {
        [Fact]
        public void OneOfTest()
        {
            True("str".IsOneOf("a", "b", "str"));
        }

        [Fact]
        public void DecomposeTest()
        {
            var str = "Hello, world";
            (int index, char ch) = str.Decompose(s => s.IndexOf(','), s => s[1]);
            Equal(5, index);
            Equal('e', ch);
            str.Decompose(s => s.IndexOf(','), s => s[1], out index, out ch);
            Equal(5, index);
            Equal('e', ch);
        }

        [Fact]
        public void UserDataStorageTest()
        {
            var slot = UserDataSlot<long>.Allocate();
            var str = new string('a', 3);
            str.GetUserData().Set(slot, 42);
            Equal(42, str.GetUserData().Get(slot));
            str = null;
            GC.Collect();
            GC.WaitForFullGCComplete();
            str = new string('a', 3);
            Equal(0, str.GetUserData().Get(slot));
        }

        [Fact]
        public void InvalidDataSlotTest()
        {
            var str = new string('b', 3);
            Throws<ArgumentException>(() => str.GetUserData().Set(new UserDataSlot<int>(), 10));
        }
    }
}