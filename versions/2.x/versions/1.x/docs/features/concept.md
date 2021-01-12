Concept Types
====
[Concept Types](https://github.com/dotnet/csharplang/issues/110) is a proposed feature of C# and not yet implemented (at least in C# 8.0). DotNext Reflection library allows to use this feature in the current version of C#.

If you are not familiar with type classes then read these articles first:
* [Type Classes](https://www.haskell.org/tutorial/classes.html) in Haskell programming language
* [Type Classes](https://en.wikipedia.org/wiki/Type_class) on Wikipedia

> [!CAUTION]
> Due to lack of native compiler support of this feature, any mistake in definition of concept type will not be highlighted at compile time.

The feature is based on strongly typed reflection so read [this](reflection/fast.md) document first. Entry point to discover class members is [Type&lt;T&gt;](../api/DotNext.Reflection.Type-1.yml). Reflected members are cached to speed-up performance and typed by specific delegate type. The delegate describes signature of the reflected member. All types of members are supported: constructors, fields, methods, event, properties, indexer properties.

# Defining Concept
The recommended code style for the concept type is a definition of static class with restrictions expressed as static fields. Let's define type class with single instance method and single static method:
```csharp
using DotNext.Reflection;
using DotNext.Runtime.CompilerServices;
using System.Runtime.CompilerServices;

[Concept]
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

[ConceptAttribute](../api/DotNext.Runtime.CompilerServices.ConceptAttribute.yml) is required attribute that should be applied to the concept type definition.

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
using DotNext.Runtime.CompilerServices;

[Concept]
public static class LengthSupport<T>
{
    private static readonly MemberGetter<T, int> lengthProp = Type<T>.Property<int>.Require("Length");

    public static int GetLength(in T @this) => lengthProp(@this);
}

var length = LengthSupport<string>.GetLength("Hello, world!"); //length == 13
length = LengthSupport<byte[]>.GetLength(new byte[]{ 1, 2, 3 }); //length == 3
```

Now this concept allows to obtain value of `Length` property from any object.

`Require` method is used to obtain instance member from the type specified as actual generic argument for type `Type<T>`. Static member can be obtained using `RequireStatic` family of methods. `Require` and `RequireStatic` are useful in the context of concept type declaration. If target type _T_ doesn't have required member then these methods will throw one of [ConstraintViolationException](../api/DotNext.Reflection.ConstraintViolationException.yml) ancestors:
* [MissingConstructorException](../api/DotNext.Reflection.MissingConstructorException.yml) if required constructor doesn't exist
* [MissingMethodException](../api/DotNext.Reflection.MissingMethodException.yml) if required method or property getter/setter doesn't exist
* [MissingFieldException](../api/DotNext.Reflection.MissingFieldException.yml) if required field doesn't exist
* [MissingEventException](../api/DotNext.Reflection.MissingEventException.yml) if required event doesn't exist

In the context of strongly typed reflection it is recommended to use alternative methods `Get` (for instance members) or `GetStatic` (for static members). These methods have same the same behavior as `Require`/`RequireStatic` but they don't throw exception. If member cannot be resolved then these methods return **null**.

# Applying Concept
When concept type is declared, it can be used as a constraint for generic type parameter of class or method. [Concept](../api/DotNext.Runtime.CompilerServices.Concept.yml) class allows to apply concept type to the generic parameter and verify constraints, thus, _fail fast_ if actual generic argument doesn't meet to the them. [ConstraintAttribute](../api/DotNext.Runtime.CompilerServices.ConstraintAttribute.yml) indicating that the specified generic parameter is constrained with one or more concept types.
```csharp
using DotNext.Runtime.CompilerServices;
using System;

public sealed class Formatter<[Constraint(typeof(Parseable<>))] T>
{
    static Formatter() => Concept.Assert(typeof(Parseable<T>));

    private readonly string format;

    public Formatter(string fmt) => format = fmt;

    public string Concat(in T first, in T second) 
        => Parsable<T>.ToString(first, format) + Parseable<T>.ToString(second, format);
}

var formatter = new Formatter<long>();          //constraint checked here
Console.WriteLine(formatter.Concat(1L, 2L));
```

Here, actual type `T` must satisfy to constraints defined by `Parseable` concept type, i.e. to have one instance method and one static method. `ConstraintAttribute` is an optional attribute aimed to inform the developer that generic parameter is constrained by one or more concept types. CLR doesn't rely on this attribute as well as Roslyn compiler. `Concept.Assert` enforces verification of generic argument correctness according with concept type. There are few reasons to place assertion into static constructor:
1. Actual generic argument is accessible from static constructor
1. Assertion is called automatically before the first instance is created or any static members are referenced
1. Static constructor called once

Invalid generic argument causes exception of type `ConstraintViolationException`.

However, assertion is optional and not recommended for use inside of generic methods. Let's remove assertion code from the static constructor. In this case, verfication will be performed only at the following line of code:
```csharp
var formatter = new Formatter<long>();          
Console.WriteLine(formatter.Concat(1L, 2L));//constraint checked here
```

This is contradiction to _fail fast_ strategy because `Formatter` class can be instantiated with wrong actual generic argument.

# Special Delegates
`Type<T>` and its nested classes offer a rich set of methods for members binding. These methods reflect members as well-known delegate types defined in .NET library or DotNext Reflection library. In some cases, no one of these delegates can fit the requested member. For example, overloaded method [int.TryParse](https://docs.microsoft.com/en-us/dotnet/api/system.int32.tryparse) with two parameters has **out** parameter. In this case, the supported set of delegates will not help. This issue can be resolved in two ways:
* Use custom delegate type, as it was shown in the example above (`ToStringMethod` delegate type)
* Use special delegates provided by DotNext Reflection library:
    * [Function&lt;A, R&gt;](../api/DotNext.Function-2.yml) for static methods with return type
    * [Function&lt;T, A, R&gt;](../api/DotNext.Function-3.yml) for instance methods with return type
    * [Procedure&lt;A&gt;](../api/DotNext.Procedure-1.yml) for static methods without return type
    * [Procedure&lt;T, A&gt;](../api/DotNext.Procedure-2.yml) for instance methods without return type

These delegates able to represent the signature of any requested method and handled by `Type<T>` differently in comparison with regular delegate types.

> [!NOTE]
> ref-like structs are not supported by these delegates because it is forbidden by compiler to pass such data types as actual generic arguments

These delegates accept input arguments in the form of the value type. Usually, the value type for the arguments is initialized on the stack. Therefore, all arguments will be passed through stack and .NET optimizations related to arguments passing are not possible.

It is allowed to use any custom value type to pass arguments. The arguments should be represented by public instance fields. Properties and private fields will be ignored by `Type<T>`. Therefore, it is recommended to use value tuples for passing arguments. Value tuples have native compiler support in C# and VB.NET so the source code still remain readable.

If parameter in the method signature is declared as **ref** our **out** then field in such structure should be of type [Ref&lt;T&gt;](../api/DotNext.Reflection.Ref-1.yml).

```csharp
using DotNext.Reflection;

//reflect static method as Function
Function<(string text, Ref<decimal> result), bool> tryParse = Type<decimal>.GetStaticMethod<(string, Ref<decimal>), bool>(nameof(decimal.TryParse));
//allocate arguments on the stack
(string text, Ref<decimal> result) args = default;  //or use var args = tryParse.ArgList(); with the same result
args.text = "42";
tryParse(args);
decimal parsedValue = args.result;  //parsedValue == 42M
```

Let's assume that type of `text` parameter is unknown or unreachable from source code. In this case, it is possible to use **object** type with some performance overhead (but still much faster than .NET reflection):

```csharp
using DotNext.Reflection;

//reflect static method as Function
Function<(object text, Ref<decimal> result), bool> tryParse = Type<decimal>.GetStaticMethod<(object, Ref<decimal>), bool>(nameof(decimal.TryParse));
//allocate arguments on the stack
(object text, Ref<decimal> result) args = default;  //or use var args = tryParse.ArgList(); with the same result
args.text = "42";
tryParse(args);
decimal parsedValue = args.result;  //parsedValue == 42M
```

# Reusable Concepts
The library offers ready-to-use concept types:
* [Number&lt;T&gt;](../api/DotNext.Number-1.yml) represents any numeric type. This concept exposes operators, instance and static methods that are common to all numeric types in .NET Base Class Library.
* [Disposable](../api/DotNext.Disposable-1.yml) represents any type implementing _Dispose pattern_ even if target type doesn't implement [IDisposable](https://docs.microsoft.com/en-us/dotnet/api/system.idisposable) interface directly.
* [Awaitable&lt;T, TAwaiter&gt;](../api/DotNext.Runtime.CompilerServices.Awaitable-2.yml) or [Awaitable&lt;T, TAwaiter, R&gt;](../api/DotNext.Runtime.CompilerServices.Awaitable-3.yml) represents _awaitable pattern_ which describes any type compatible with **await** operator.

