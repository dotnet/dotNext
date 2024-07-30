namespace DotNext;

public sealed class BasicExtensionsTests : Test
{
    private sealed class UserDataSupport : UserDataStorage.IContainer
    {
        private readonly object source;

        internal UserDataSupport() => source = DotNext.UserDataStorage.IContainer.CreateStorage();

        internal UserDataSupport(object source) => this.source = source;

        object UserDataStorage.IContainer.Source => source;
    }

    [Fact]
    public static void UserDataStorage()
    {
        var slot = new UserDataSlot<long>();
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
        var slot = new UserDataSlot<long>();
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
        var slot = new UserDataSlot<long>();
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
        var slot = new UserDataSlot<long>();
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
    public static void RegressionIssue248()
    {
        var slot = new UserDataSlot<long>();
        var str1 = new string('a', 3);
        var str2 = new string('b', 3);
        NotSame(str1, str2);
        str2.GetUserData().Set(slot, 42L);
        str1.GetUserData().CopyTo(str2);

        False(str2.GetUserData().TryGet(slot, out _));
    }

    [Fact]
    public static void UserDataStorageGetOrSet()
    {
        static string ToStr(int value) => value.ToString();

        var obj = new object();
        var slot = new UserDataSlot<string>();
        Equal("42", obj.GetUserData().GetOrSet(slot, 42, ToStr));
    }

    [Fact]
    public static void UserDataStorageGetOrSetSimpleFactory()
    {
        static string CreateString() => "Hello, world!";

        var obj = new object();
        var slot = new UserDataSlot<string>();
        Equal("Hello, world!", obj.GetUserData().GetOrSet(slot, CreateString));
    }

    [Fact]
    public static void InvalidDataSlot()
    {
        var str = new string('b', 3);
        Throws<ArgumentException>(() => str.GetUserData().Set(default(UserDataSlot<int>), 10));
    }

    [Fact]
    public static void ResizeSlotsOfTheSameType()
    {
        var slot1 = new UserDataSlot<ulong>();
        var slot2 = new UserDataSlot<ulong>();
        var slot3 = new UserDataSlot<ulong>();
        var slot4 = new UserDataSlot<ulong>();

        var data = new object().GetUserData();
        data.Set(slot1, 42UL);
        data.Set(slot2, 43UL);
        data.Set(slot3, 44UL);
        data.Set(slot4, 45UL);

        Equal(42UL, data.Get(slot1));
        Equal(43UL, data.Get(slot2));
        Equal(44UL, data.Get(slot3));
        Equal(45UL, data.Get(slot4));
    }

    [Fact]
    public static void ResuzeSlotsOfDifferentTypes()
    {
        var slot1 = new UserDataSlot<ulong>();
        var slot2 = new UserDataSlot<ushort>();
        var slot3 = new UserDataSlot<short>();
        var slot4 = new UserDataSlot<uint>();
        var slot5 = new UserDataSlot<sbyte>();

        var data = new object().GetUserData();
        data.Set(slot1, 42UL);
        data.Set(slot2, (ushort)43);
        data.Set(slot3, (short)44);
        data.Set(slot4, 45U);
        data.Set(slot5, (sbyte)46);

        Equal(42UL, data.Get(slot1));
        Equal(43, data.Get(slot2));
        Equal(44, data.Get(slot3));
        Equal(45U, data.Get(slot4));
        Equal(46, data.Get(slot5));
    }

    [Fact]
    public static void CaptureData()
    {
        var slot1 = new UserDataSlot<ulong>();
        var slot2 = new UserDataSlot<ushort>();
        var slot3 = new UserDataSlot<short>();
        var slot4 = new UserDataSlot<uint>();
        var slot5 = new UserDataSlot<sbyte>();

        var data = new object().GetUserData();
        True(data.IsValid);
        data.Set(slot1, 42UL);
        data.Set(slot2, (ushort)43);
        data.Set(slot3, (short)44);
        data.Set(slot4, 45U);
        data.Set(slot5, (sbyte)46);

        var capturedData = data.Capture();

        Equal(42UL, capturedData[slot1.ToString()]);
        Equal((ushort)43, capturedData[slot2.ToString()]);
        Equal((short)44, capturedData[slot3.ToString()]);
        Equal(45U, capturedData[slot4.ToString()]);
        Equal((sbyte)46, capturedData[slot5.ToString()]);
    }

    [Fact]
    public static void OneOfValues()
    {
        True(2.IsOneOf(stackalloc int[] { 2, 5, 7 }));
        False(2.IsOneOf(stackalloc int[] { 3, 5, 7 }));
    }

    [Fact]
    public static void Range()
    {
        True(15M.IsBetween(10M.Enclosed(), 20M.Enclosed()));
        False(10M.IsBetween(10M.Disclosed(), 20M.Disclosed()));
        True(10M.IsBetween(10M.Enclosed(), 20M.Disclosed()));
        False(15M.IsBetween(10M.Enclosed(), 12M.Enclosed()));
    }

    [Fact]
    public static void LeftGreaterThanRight()
    {
        False(4L.IsBetween(4L.Enclosed(), 3L.Enclosed()));
        False(4L.IsBetween(4L.Enclosed(), 4L.Disclosed()));
    }

    [Fact]
    public static void InfinityEndpoints()
    {
        True(4L.IsBetween(IRangeEndpoint<long>.Infinity, 10L.Enclosed()));
        False(10L.IsBetween(IRangeEndpoint<long>.Infinity, 10L.Disclosed()));

        True(4L.IsBetween(0L.Disclosed(), IRangeEndpoint<long>.Infinity));
        False(10L.IsBetween(10L.Disclosed(), IRangeEndpoint<long>.Infinity));

        True(long.MinValue.IsBetween(IRangeEndpoint<long>.Infinity, IRangeEndpoint<long>.Infinity));
    }
}