Typed Reference
====
C# has `ref` keyword that allows to pass the location in the memory where the value is stored. Under the hood, at IL level it is represented by special type called _managed pointer_ indicated as _T&amp;_ (in opposite to unmanaged pointer _T*_). Internally, .NET BCL allows to declare the field of managed pointer type using [ByReference&lt;T&gt;](https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/ByReference.cs) data type. [ReadOnlySpan&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.readonlyspan-1) and other **ref** structs built on top of `ref` fields. Parameters of managed pointer or **ref** struct type cannot be declared for asynchronous methods or stored in the fields of regular classes or value types.

[Reference&lt;T&gt;](xref:DotNext.Runtime.Reference`1) data type is a regular value type that allows to represent the memory location of the value without restriction of **ref** struct. As a result, you can use it in asynchronous methods or store in the field.

There is no magic under the hood and no violation of GC tracking rules. Under the hood, `Reference<T>` efficiently encapsulates the access to the memory location. You can obtain a reference to the following things:
* A reference to the array element
* A reference to the static field
* A reference to the instance field
* A reference to the value allocated in the unmanaged memory (act as a wrapper for unmanaged pointer)
* A reference to boxed value type

There is no way to obtain a reference to the local variable. Technically, it is possible but very dangerous because the lifetime of the reference may be greater than the lifetime of the local variable.

[Reference&lt;T&gt;](xref:DotNext.Runtime.Reference`1) can be constructed using the factory methods exposed by [Reference](xref:DotNext.Runtime.Reference) static class:
```csharp
using DotNext.Runtime;

// obtains a reference to the first element of the array
int[] array = { 10, 20, 30 };
Reference<int> r = Reference.ArrayElement(array, 0);
r.Target = 20; // mutates the array element

class MyClass
{
    internal static int StaticField;

    internal int field;

    static ref int GetInstanceFieldReference(MyClass obj) => ref obj.field;

    static ref int GetStaticFieldReference() => ref StaticField;
}

// instance field
Reference<int> r = Reference.Create<MyClass, int>(new MyClass(), &MyClass.GetInstanceFieldReference);
r.Target = 20; // mutates the instance field

// static field
Reference<int> r = Reference.Create<int>(&MyClass.GetStaticFieldReference);
r.Target = 20; // mutates the static field

// obtains a reference to the boxed value
object boxed = 42;
Reference<int> r = Reference.Unbox<int>(boxed);
```

Additionally, `Reference` allows to allocate anonymous memory storage of the specified type:
```csharp
using DotNext.Runtime;

Reference<string> r = Reference.Allocate<string>(string.Empty);
r.Target = "Hello, world!";
```

The allocated storage can be used to pass the value by reference to asynchronous method.