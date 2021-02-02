Fast Reflection
====
Invocation of reflected members in .NET is slow. This happens because late-binding [invocation](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.methodbase.invoke) should provide type check of arguments for each call. DotNext Reflection library provides a way to invoke reflected members in strongly typed manner. It means that invocation parameters are typed and type safety is guaranteed by compiler. Moreover, this feature allows to invoke members with the same performance as they called without reflection. The reflected member can be converted into appropriate delegate instance for caching and further invocation. The binding process still performed dynamically and based on .NET reflection.

[Reflector](xref:DotNext.Reflection.Reflector) class provides methods for reflecting class members. The type of delegate instance which represents reflected member describes the signature of the method or constructor. But what the developer should do if one of constructor or method parameteters has a type that is not visible from the calling code (e.g. type has **internal** visibility modifier and located in third-party library)? This issue is covered by Reflection library with help of the following special delegate types:
* [Function&lt;A, R&gt;](xref:DotNext.Function`2) for static methods with return type
* [Function&lt;T, A, R&gt;](xref:DotNext.Function`3) for instance methods with return type
* [Procedure&lt;A&gt;](xref:DotNext.Procedure`1) for static methods without return type
* [Procedure&lt;T, A&gt;](xref:DotNext.Procedure`2) for instance methods without return type

These delegates can describe signature of arbitrary methods or constructors with a little performance cost: all arguments will passed through stack. As a result, they can be used if developer don't want to introduce a new delegate type for some untypical signatures (with **ref** or **out** parameters).

Combination of various delegate signatures and `Reflector` class provide configurable approach to fast reflection and allows to choose between convenience and performance. Moreover, it requires compile-time some knowledge about underlying types of parameters and declaring type. To reduce this complexity, .NEXT Reflection library offers lightweight fast reflection API represented by non-generic overloaded version of `Unreflect` extension method. Lightweight implementation is a dynamic compilation of member access code and exposes unified API surface for all supported member types in the form of single [DynamicInvoker](xref:DotNext.Reflection.DynamicInvoker) delegate type. Invocation API is very similar to reflection provided by .NET out-of-the-box, but much more faster. 

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
using var stream = ctor(args);
```

Moreover, it is possible to use custom delegate type for reflection:
```csharp
using DotNext.Reflection;
using System.IO;

internal delegate MemoryStream MemoryStreamConstructor(byte[] buffer, bool writable);

MemoryStreamConstructor ctor = typeof(MemoryStream).GetConstructor(new[] { typeof(byte[]), typeof(bool) }).Unreflect<MemoryStreamConstructor>();
using var stream = ctor(new byte[] { 1, 10, 5 }, false);
```

Lightweight object construction can be achieved using overloaded non-generic `Unreflect` method:
```csharp
using DotNext.Reflection;
using System.IO;

var ctor = typeof(MemoryStream).GetConstructor(new[] { typeof(byte[]), typeof(bool) }).Unreflect();
using var stream = (MemoryStream)ctor(null, new byte[] { 1, 10, 5}, false);
```

# Method
Static or instance method can be reflected as delegate instance. In case of instance method, first argument of the delegate should accept **this** argument:
* _T_ for reference type
* _ref T_ for value type

```csharp
using System.Numerics;
using DotNext.Reflection;

internal delegate byte[] ToByteArray(ref BigInteger @this);

var toByteArray = typeof(BigInteger).GetMethod(nameof(BigInteger)).Unreflect<ToByteArray>();
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

Let's assume than type of `text` parameter is not known at compile time or unreachable from source code because the type is declared in external library and has **internal** visibility modifier. In this case, the type of such parameter can be replaced with **object** data type. Of course, it will affect performance but still be much faster than classic .NET reflection.
```csharp
using DotNext.Reflection;

Function<(object text, decimal result), bool> tryParse = typeof(decimal).GetMethod(nameof(decimal.TryParse), new[]{typeof(string), typeof(decimal).MakeByRefType()}).Unreflect<Function<(object, decimal), bool>>();

(object text, decimal result) args = tryParse.ArgList();
args.text = "42";
tryParse(args);
decimal v = args.result;    //v == 42M
```

Lightweight method invocation can be achieved using overloaded non-generic `Unreflect` method:
```csharp
using DotNext.Reflection;

var tryParse = typeof(decimal).GetMethod(nameof(decimal.TryParse), new[]{typeof(string), typeof(decimal).MakeByRefType()}).Unreflect();
object[] args = {"42", decimal.Zero};
tryParse(null, args);
decimal v = (decimal)args[1];
```

# Field
Static or instance field can obtained from [FieldInfo](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.fieldinfo) using `Unreflect` extension method declared in [Reflector](xref:DotNext.Reflection.Reflector) class. This feature gives the power to work with field values using Reflection without performance loss.

> [!IMPORTANT]
> Managed pointer to the field value is mutable even if field is **readonly**. As a result, you can modify value of such field. It is responsibility of the developer to control access to read-only fields.

This is not the only way to obtain direct access to the field. [Field&lt;V&gt;](xref:DotNext.Reflection.Field`1) and [Field&lt;T,V&gt;](xref:DotNext.Reflection.Field`2) that can be returned by [Type&lt;T&gt;.Field&lt;T&gt;](xref:DotNext.Reflection.Type`1.Field`1) provide access to static and field value respectively.

The following example demonstrates how to obtain managed pointer to the static and instance fields:
```csharp
using DotNext.Reflection;
using System.Reflection;

class MyClass
{
	private static long StaticField;

	private string instanceField;

	public MyClass(string str) => instanceField = str;
}

//change value of static field
ref long staticField = ref typeof(MyClass).GetField("StaticField", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly).Unreflect<long>().Value;
staticField = 42L;

//change value of instance field
var obj = new MyClass();
ref string instanceField = ref obj.GetClass().GetField("instanceField", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).Unreflect<string>()[obj];
instanceField = "Hello, world";
```

Lightweight field access can be achieved using overloaded non-generic `Unreflect` method which supports various optimization:
* Obtain field getter only
* Obtain field setter only
* Obtain field setter and getter combined into single instance of [DynamicInvoker](xref:DotNext.Reflection.DynamicInvoker)

Third option is slower in comparison with others. Therefore if you expect one-directional access to the field then use proper optimization.

The following example demonstrates all possible optimization modes when generating field accessor:
```csharp
using DotNext.Reflection;
using System;
using System.Reflection;

var obj = new MyClass("Hello, world!");
//generate read-only accessor
var field = obj.GetClass().GetField("instanceField", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
DynamicInvoker invoker = field.Unreflect(BindingFlags.GetField);	
Console.WriteLine(invoker(obj));	//prints Hello, world!

//generate write-only accessor
invoker = field.Unreflect(BindingFlags.SetField);
invoker(obj, "New field value");	//obj.instanceField = "New field value"

//generate read-write accessor
invoker = field.Unreflect();
invoker(obj, "Hello, world!");
Console.WriteLine(invoker(obj));	//prints Hello, world!
```

# Performance
Invocation of members through special delegates is not a free lunch: you pay for passing arguments through the stack. But it still much faster than classic .NET Reflection. The following list describes performance impact using different approaches to reflection (from fast to slow).

| Reflective call | Performance |
| ---- | ---- |
| Custom delegate type or predefined delegate type which exactly describes the signature of expected method | the same or comparable to direct call (with nanoseconds overhead) |
| Special delegate types | x1,4 slower than direct call |
| Special delegate types with one or more unknown parameter types (when **object** used instead of actual type) | x2/x3 slower than direct call |
| Dynamically compiled _DynamicInvoker_ | x1.5/x2 slower than direct call and causes heap allocation of arguments of value type |
| Classic .NET Reflection | x10/x50 slower than direct call |

Read more about performance in [Benchmarks](../../benchmarks.md) article.