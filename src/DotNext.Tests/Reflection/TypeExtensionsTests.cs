using System.Reflection;

namespace DotNext.Reflection;

public sealed class TypeExtensionsTests : Test
{
    public sealed class MyList : List<string>
    {

    }

    [Fact]
    public static void DelegateSignature()
    {
        var signature = DelegateType.GetInvokeMethod<Func<int, string>>();
        NotNull(signature);
        Equal(typeof(int), signature.GetParameters()[0].ParameterType);
        Equal(typeof(string), signature.ReturnParameter.ParameterType);
    }

    [Fact]
    public static void IsGenericInstanceOf()
    {
        True(typeof(Func<string>).IsGenericInstanceOf(typeof(Func<>)));
        False(typeof(Func<string>).IsGenericInstanceOf(typeof(Func<int>)));
        True(typeof(List<int>).IsGenericInstanceOf(typeof(List<>)));
        True(typeof(MyList).IsGenericInstanceOf(typeof(IEnumerable<>)));
    }

    [Fact]
    public static void CollectionElement()
    {
        Equal(typeof(string), typeof(MyList).GetItemType(out var enumerable));
        Equal(typeof(IEnumerable<string>), enumerable);
        Equal(typeof(int), typeof(int[]).GetItemType(out enumerable));
        Equal(typeof(IEnumerable<int>), enumerable);
    }

    private struct ManagedStruct
    {
        internal int value;
        internal string name;

        internal ManagedStruct(int value, string name)
        {
            this.value = value;
            this.name = name;
        }
    }

    private static int SizeOf<T>() where T : unmanaged => System.Runtime.CompilerServices.Unsafe.SizeOf<T>();

    [Fact]
    public static void IsUnmanaged()
    {
        True(typeof(IntPtr).IsUnmanaged());
        True(typeof(UIntPtr).IsUnmanaged());
        True(typeof(bool).IsUnmanaged());
        True(typeof(Guid).IsUnmanaged());
        True(typeof(DateTime).IsUnmanaged());
        False(typeof(Runtime.InteropServices.Pointer<int>).IsUnmanaged());
        False(typeof(ManagedStruct).IsUnmanaged());
        var method = new Func<int>(SizeOf<long>).Method;
        method = method.GetGenericMethodDefinition();
        True(method.GetGenericArguments()[0].IsUnmanaged());
    }

    [Fact]
    public static unsafe void IsImmutable()
    {
        True(typeof(ReadOnlySpan<int>).IsImmutable());
        True(typeof(Guid).IsImmutable());
        True(typeof(long).IsImmutable());
        True(typeof(void*).IsImmutable());
        True(typeof(void).MakeByRefType().IsImmutable());
        True(typeof(EnvironmentVariableTarget).IsImmutable());
    }

    [Fact]
    public static void DynamicCast()
    {
        Equal(43, typeof(int).Cast(43));
        Null(typeof(string).Cast(null));
        Equal("abc", typeof(string).Cast("abc"));
        Throws<InvalidCastException>(() => typeof(int).Cast(null));
        Throws<InvalidCastException>(() => typeof(int).Cast("abc"));
    }

    [Fact]
    public static void Devirtualization()
    {
        var toStringMethod = typeof(object).GetMethod(nameof(ToString));
        var overriddenMethod = typeof(string).Devirtualize(toStringMethod);
        NotEqual(toStringMethod, overriddenMethod);
        Equal(typeof(string), overriddenMethod.DeclaringType);
    }

    [Fact]
    public static void IntefaceMethodResolution()
    {
        var toInt32Method = typeof(IConvertible).GetMethod(nameof(IConvertible.ToInt32));
        var overriddenMethod = typeof(int).Devirtualize(toInt32Method);
        NotEqual(toInt32Method, overriddenMethod);
        Equal(typeof(int), overriddenMethod.DeclaringType);
    }

    [Fact]
    public static void EqualsMethodResolution()
    {
        var getTypeMethod = typeof(object).GetMethod(nameof(GetType));
        var overriddenMethod = typeof(string).Devirtualize(getTypeMethod);
        Equal(getTypeMethod, overriddenMethod);
    }

    [Fact]
    public static void ReflectMember()
    {
        NotNull(Reflector.MemberOf<FieldInfo, Func<string>>(() => string.Empty));
        NotNull(Reflector.MemberOf<PropertyInfo, Func<Optional<string>>>(() => Optional<string>.None));
        NotNull(Reflector.MemberOf<ConstructorInfo, Func<string>>(() => new string('a', 3)));
        NotNull(Reflector.MemberOf<MethodInfo, Func<decimal, decimal>>(static x => -x));
        Null(Reflector.MemberOf<MemberInfo, Func<int, int>>(static i => -i));
    }
}