Concept Types
====
[Concept Types](https://github.com/dotnet/csharplang/issues/110) is a proposed feature of C# and not yet implemented (at least in C# 8.0). DotNext Reflection library allows to use this feature in the current version of C#.

If you are not familiar with type classes then read these articles first:
* [Type Classes](https://www.haskell.org/tutorial/classes.html) in Haskell programming language
* [Type Classes](https://en.wikipedia.org/wiki/Type_class) on Wikipedia

> [!CAUTION]
> There is no native compiler support of this feature, therefore, if you have a mistake in your concept definition it will not highlighted at compile time.

The feature is based on strongly typed reflection so read [this](reflection/fast.md) document first. Starting point to discover class members is [Type&lt;T&gt;](../api/DotNext.Reflection.Type-1.yml). Reflected members are cached to speed-up performance and typed by specific delegate type. The delegate describes signature of the reflected member. All types of members are supported: constructors, fields, methods, event, properties, indexer properties.

# Defining Concept
The recommended code style for the concept type is a definition of static class with restrictions expressed as static fields. Let's define type class with single instance method and single static method:
```csharp
using System.Runtime.CompilerServices;
using DotNext.Reflection;

public static class Parseable<T>
    where T: struct
{
    private delegate string ToStringMethod(in T @this, string format);

    private static readonly Func<string, T> parseMethod = Type<T>.Method.Require<Func<string, T>>("Parse", MethodLookup.Static);
    private static readonly ToStringMethod toStringMethod = Type<T>.Method.Require<ToStringMethod>("ToString", MethodLookup.Instance);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Parse(string text) => parseMethod(text);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToString(in T @this, string format) => toStringMethod(@this, format);
}
``` 

`Type<T>.Method.Require` is a method requirement declaration. In this example, type _T_ should have two methods:
* Public static method `T Parse(string text)`
* Public instance method `string ToString()`

Existence of these methods checked at runtime (because no native support from C# compiler) when `Parseable` generic type is instantiated with actual generic arguments.

Now this concept can be used to call declared methods:
```csharp
int i = Parseable<int>.Parse("123"); //i == 123
string str = Parseable<int>.ToString(i, "X"); //str == "0x7B"
```

The concept type allows to call static and instance members of type passed into generic parameter when it is not possible to predict actual generic argument. 

The following concept type requires to have public instance property `Length` of type `int`.

```csharp
using DotNext.Reflection;

public static class LengthSupport<T>
{
    private static readonly MemberGetter<T, int> lengthProp = Type<T>.Property<int>.Require("Length");

    public static int GetLength(in T @this) => lengthProp(@this);
}

var length = LengthSupport<string>.GetLength("Hello, world!"); //length == 13
length = LengthSupport<byte[]>.GetLength(new byte[]{ 1, 2, 3 }); //length == 3
```

Now this concept allows to obtain value of `Length` property from any object.

# Reusable Concepts
The library offers ready-to-use concept types:
* [Number&lt;T&gt;](../api/DotNext.Number-1.yml) represents any numeric type. This concept exposes operators, instance and static methods that are common to all numeric types in .NET Base Class Library.
* [Disposable](../api/DotNext.Disposable-1.yml) represents any type implementing _Dispose pattern_ even if target type doesn't implement [IDisposable](https://docs.microsoft.com/en-us/dotnet/api/system.idisposable) interface directly.
* [Awaitable&lt;T, TAwaiter&gt;](../api/DotNext.Runtime.CompilerServices.Awaitable-2.yml) or [Awaitable&lt;T, TAwaiter, R&gt;](../api/DotNext.Runtime.CompilerServices.Awaitable-2.yml) represents_awaitable pattern_ which describes any type compatible with **await** operator.

