Intrinsics
====
Intrinsic methods are special kind of methods whose implementation is handled specially by JIT compiler or written in pure IL code to achieve low (or zero, in some situations) overhead. .NET library has good example in the form of [Unsafe](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.unsafe) class which is implemented in pure IL. The implementation in pure IL allows to be closer to the bare metal and utilize code patterns recognized by JIT compiler. 

.NEXT library has numerous intrinsics which are exposed by [Intrinsics](xref:DotNext.Runtime.Intrinsics) class and grouped into the following categories:
* Common intrinsics which have very low or zero runtime overhead
* Low-level memory manipulations

# Common Intrinsics
Common intrinsics are various methods aimed to increase performance in some situations.

`AlignOf<T>` method is a macros that determines the alignment of the specified type.

`AreCompatible` methid is a macros that determines binary compatibility of two types passed as generic arguments. The two types are compatible if
* They have the same alignment
* They have the same size

For instance, **byte** and **sbyte** are compatible.

`IsDefault` method is the most useful method that allows to check whether the value of type `T` is `default(T)`. It works for both value type and reference type.

```csharp
using DotNext.Runtime;

Intrinsics.IsDefault("");   //false
Intrinsics.IsDefault(default(string));  //true
Intrinsics.IsDefault(0);    //true
Intrinsics.IsDefault(1);    //false
```

# Exact Type-Testing
The **is** operator in C# checks if the result of an expression is compatible with a given type. However, in some rare cases you need to check whether the object is of exact type. `IsExactTypeOf` method provides optimized implementation of this case:
```csharp
"a" is object;  //true
"a" is string;  //true
Intrinsics.IsExactTypeOf<object>("a");  //false
Intrinsics.IsExactTypeOf<string>("a");  //true
```

# Array Length
C# 9 introduces primitive type `nuint` that can be used for accessing array elements without hidden conversion of the index. However, there is no way to obtain array length as native integer. `Intrinsics.GetLength` provides this ability so the loop over array elements can be rewritten as follows:
```csharp
using DotNext.Runtime;

var array = new int[] { ... };
for (nuint i = 0; i < Intrinsics.GetLength(array); i++)
{
    
}
```