namespace DotNext.Reflection
{
    public sealed class TypeExtensionsTests : Assert
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
        }

        private static void GenericMethod<T>(T arg, int i)
        {

        }

        [Fact]
        public static void GetGenericMethod()
        {
            var method = typeof(Task).GetMethod(nameof(Task.FromException), BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly, 0, typeof(Exception));
            NotNull(method);
            method = typeof(Task).GetMethod(nameof(Task.FromException), BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly, 1, typeof(Exception));
            NotNull(method);
            method = typeof(TypeExtensionsTests).GetMethod(nameof(GenericMethod), BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly, 1, null, typeof(int));
            NotNull(method);
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
        public static void IsImmutable()
        {
            True(typeof(ReadOnlySpan<int>).IsImmutable());
            False(typeof(Guid).IsImmutable());
            True(typeof(long).IsImmutable());
        }
    }
}
