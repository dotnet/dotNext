Fast Reflection
====
Invocation of reflected members in .NET is slow. This happens because late-binding [invocation](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.methodbase.invoke) should provide type check of arguments for each call. DotNext Reflection library provides a way to invoke reflected members in strongly typed manner. It means that invocation parameters are typed and type safety is guaranteed by compiler. Moreover, this feature allows to invoke members with the same performance as they called without reflection. The reflected member can be converted into appropriate delegate instance for caching and further invocation. The reflection process are still performed dynamically and based on .NET reflection.

[Reflector](../../api/DotNext.Reflection.Reflector.yml) class provides methods for reflecting class members. The type of delegate instance which represents reflected member describes the signature of the method or constructor. But what the developer should do if one of constructor or method parameteters has a type that is not visible from the calling code (e.g. type has **internal** visibility modifier and located in third-party library)? This issue is covered by Reflection library with help of the following special delegate types:
* [Function&lt;A, R&gt;](../../api/DotNext.Function-2.yml) for static methods with return type
* [Function&lt;T, A, R&gt;](../../api/DotNext.Function-3.yml) for instance methods with return type
* [Procedure&lt;A&gt;](../../api/DotNext.Procedure-1.yml) for static methods without return type
* [Procedure&lt;T, A&gt;](../../api/DotNext.Procedure-2.yml) for instance methods without return type

These delegates can describe signature of arbitrary methods or constructors with a little performance cost: all arguments will passed through stack. As a result, they can be used if developer don't want to introduce a new delegate type for some untypical signatures (with **ref** or **out** parameters).

# Constructor
Constructor can be reflected as delegate instance.
```csharp
using System.IO;
using DotNext.Reflection;

Func<byte[], bool, MemoryStream> ctor = typeof(MemoryStream).GetConstructor(new[] { typeof(byte[]), typeof(bool) }).Unreflect<Func<byte[], bool, MemoryStream>>();
using(var stream = ctor(new byte[] { 1, 10, 5 }, false))
{

}
```

The same behavior can be achieved using _Function_ special delegate:
```csharp
using System.IO;
using DotNext.Reflection;

Function<(byte[] buffer, bool writable), MemoryStream> ctor = typeof(MemoryStream).GetConstructor(new[] { typeof(byte[]), typeof(bool) }).Unreflect<Function<(byte[], bool), MemoryStream>>();

var args = ctor.ArgList();
args.buffer = new byte[] { 1, 10, 5 };
args.writable = false;
using(var stream = ctor(args))
{

}
```

Moreover, it is possible to use custom delegate type for reflection:
```csharp
using DotNext.Reflection;
using System.IO;

internal delegate MemoryStream MemoryStreamConstructor(byte[] buffer, bool writable);

Func<MemoryStreamConstructor> ctor = typeof(MemoryStream).GetConstructor(new[] { typeof(byte[]), typeof(bool) }).Unreflect<MemoryStreamConstructor>();
using(var stream = ctor(new byte[] { 1, 10, 5 }, false))
{

}
```

# Method
Static or instance method can be reflected as delegate instance. In case of instance method, first argument of the delegate should accept **this** argument:
* _T_ for reference type
* _ref T_ for value type

```csharp
using System.Numerics;
using DotNext.Reflection;

internal delegate byte[] ToByteArray(ref BigInteger @this);

var toByteArray = typeof(BigInteger).GetMethod(nameof(BigInteger)).Unreflect<ToByteArrat>();
BigInteger i = 10;
var array = toByteArray(ref i);
```

If method contains **ref** our **our** parameter then then it is possible to use custom delegate, _Function_ or _Procedure_. The following example demonstrates how to use _Function_ to call a method with **out** parameter.
```csharp
using DotNext.Reflection;

Function<(string text, decimal result), bool> tryParse = typeof(decimal).GetMethod(nameof(decimal.TryParse), new[]{typeof(string), typeof(decimal).MakeByRefType()}).Unreflect<Function<(string, decimal), bool>>();

(string text, decimal result) args = tryParse.ArgList();
args.text = "42";
tryParse(args);
decimal v = args.result;    //v == 42M
```
_args_ value passed into _Function_  instance by reference and contains all necessary arguments in the form of value tuple. 