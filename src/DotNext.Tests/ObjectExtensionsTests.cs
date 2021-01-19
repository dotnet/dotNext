using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
    public sealed class ObjectExtensionsTests : Test
    {
        private sealed class UserDataSupport : UserDataStorage.IContainer
        {
            private readonly object source;

            internal UserDataSupport() => source = DotNext.UserDataStorage.IContainer.CreateStorage();

            internal UserDataSupport(object source) => this.source = source;

            object UserDataStorage.IContainer.Source => source;
        }

        [Fact]
        public static void OneOfCheck()
        {
            True("str".IsOneOf("a", "b", "str"));
            True("str".IsOneOf(new List<string> { "a", "b", "str" }));
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
            var owner = new object();
            var obj1 = new UserDataSupport(owner);
            var obj2 = new UserDataSupport(owner);
            NotSame(obj1, obj2);
            obj2.GetUserData().Set(slot, 42L);
            Equal(42L, obj1.GetUserData().Get(slot));
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
        public static void CopyDataStorage2()
        {
            var slot = UserDataSlot<long>.Allocate();
            var obj1 = new UserDataSupport();
            var obj2 = new UserDataSupport();
            NotSame(obj1, obj2);
            obj1.GetUserData().Set(slot, 42L);
            obj1.GetUserData().CopyTo(obj2);
            Equal(42L, obj2.GetUserData().Get(slot));
            obj2.GetUserData().Set(slot, 50L);
            Equal(50L, obj2.GetUserData().Get(slot));
            Equal(42L, obj1.GetUserData().Get(slot));
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