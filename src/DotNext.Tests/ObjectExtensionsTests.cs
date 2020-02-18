using System;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
    public sealed class ObjectExtensionsTests : Test
    {
        [Fact]
        public static void OneOfCheck()
        {
            True("str".IsOneOf("a", "b", "str"));
        }

        [Fact]
        public static void Decompose()
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
        public static void UserDataStorage()
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
        public static void ShareDataStorage()
        {
            var slot = UserDataSlot<long>.Allocate();
            var str1 = new string('a', 3);
            var str2 = new string('b', 3);
            NotSame(str1, str2);
            str1.GetUserData().ShareWith(str2);
            str2.GetUserData().Set(slot, 42L);
            Equal(42L, str1.GetUserData().Get(slot));
        }

        [Fact]
        public static void CopyDataStorage()
        {
            var slot = UserDataSlot<long>.Allocate();
            var str1 = new string('a', 3);
            var str2 = new string('b', 3);
            NotSame(str1, str2);
            str1.GetUserData().Set(slot, 42L);
            str1.GetUserData().CopyTo(str2);
            Equal(42L, str2.GetUserData().Get(slot));
            str2.GetUserData().Set(slot, 50L);
            Equal(50L, str2.GetUserData().Get(slot));
            Equal(42L, str1.GetUserData().Get(slot));
        }

        [Fact]
        public static void UserDataStorageGetOrSet()
        {
            string ToStr(int value) => value.ToString();

            var obj = new object();
            var slot = UserDataSlot<string>.Allocate();
            Equal("42", obj.GetUserData().GetOrSet(slot, 42, ToStr));
        }

        [Fact]
        public static void UserDataStorageGetOrSetSimpleFactory()
        {
            string CreateString() => "Hello, world!";

            var obj = new object();
            var slot = UserDataSlot<string>.Allocate();
            Equal("Hello, world!", obj.GetUserData().GetOrSet(slot, CreateString));
        }

        [Fact]
        public static void InvalidDataSlot()
        {
            var str = new string('b', 3);
            Throws<ArgumentException>(() => str.GetUserData().Set(new UserDataSlot<int>(), 10));
        }
    }
}