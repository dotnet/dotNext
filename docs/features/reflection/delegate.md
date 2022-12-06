Open and Closed Delegates
====
Common Language Runtime supports [open and closed delegates](https://docs.microsoft.com/en-us/dotnet/api/system.delegate.createdelegate#System_Delegate_CreateDelegate_System_Type_System_Object_System_String_System_Boolean_) but there is no native support of them at the syntax level in C#. Java supports [method references](https://docs.oracle.com/javase/tutorial/java/javaOO/methodreferences.html) to do that. C# syntactically supports closed delegate based created from instance method or open delegate created from static method. 

Additionally, there is no way to create a delegate instance for a property getter, indexer or overloaded implicit/explicit type cast operator. [DelegateHelpers](xref:DotNext.DelegateHelpers) class offers two methods to provide necessary syntactic sugar:
* `CreateOpenDelegate` to create open delegate based on instance method, property, indexer, or overloaded operator
* `CreateClosedDelegateFactory` to create closed delegate based on static method, indexer, or overloaded operator

# Open Delegates
Open delegate allows to pass **this** argument explicitly. The only way to do that in C# is to use lambda expression or static method to express this capability:
```csharp
using System.Collections;

var sizeOf = new Func<ICollection, int>(collection => collection.Count);
```

This approach generates unecessary hidden static method which is used to create instance of `Predicate` delegate. `CreateOpenDelegate` allows to create open delegate without counter-intuitive usage of [Delegate.CreateDelegate](https://docs.microsoft.com/en-us/dotnet/api/system.delegate.createdelegate#System_Delegate_CreateDelegate_System_Type_System_Object_System_String_System_Boolean_).

```csharp
using DotNext;
using System.Collections;

var sizeOf = DelegateHelpers.CreateOpenDelegate<Func<ICollection, int>>(collection => collection.Count);
```

`sizeOf` delegate instance representing `get_Count` getter method directly instead of wrapping it into static method.

> [!WARNING]
> `CreateOpenDelegate` method utilizes Expression Trees feature of C# programming language. There is no free lunch and you paying for such a convenience by performance when instantiating delegate instead of `CreateDelegate`. That's why it is recommended to create such delegates statically and save them into **static readonly** fields. However, `CreateOpenDelegate` doesn't use dynamic code compilation feature.

This method is suitable for capturing overloaded operator.

```csharp
var decimalToInt = DelegateHelpers.CreateOpenDelegate<Func<int, decimal>>(i => (decimal) i);

decimal d = decimalToInt(42);
```

Built-in operators cannot be captured as delegates and method returns **null**.

# Closed Delegates
Closed delegates allow to pass first argument to the underlying method implicitly (through [Target](https://docs.microsoft.com/en-us/dotnet/api/system.delegate.target#System_Delegate_Target) property). The only way to create closed delegate in C# is to use extension method or closure:

```csharp
var str = "Hello, world!";
var isEmpty = new Func<bool>(() => string.IsNullOrEmpty(str));
```

Roslyn Compiler generates anonymous type to capture `str` local variable and instance method in such type that matches to signature of delegate `Func<bool>`.

You can avoid this with some trick from .NEXT library:
```csharp
using DotNext;

var str = "Hello, world";
Func<bool> isEmpty = new Func<string, bool>(string.IsNullOrEmpty).Method.CreateDelegate<Func<bool>>(str);
```

Now it looks better, but we loosing readability of code. The same behavior can be achieved using `CreateClosedDelegateFactory`:

```csharp
using DotNext;

var str1 = "";
var str2 = "Hello, world";

var factory = DelegateHelpers.CreateClosedDelegateFactory<Func<bool>>(() => string.IsNullOrEmpty(default(string)));
Func<bool> isEmpty1 = factory(str1);
Func<bool> isEmpty2 = factory(str2);
```

The factory returned by method `CreateClosedDelegateFactory` can be used to create closed delegate instances through passing first implicit argument to it. This approach doesn't produce anonymous type. Instead, captured argument is stored in [Target](https://docs.microsoft.com/en-us/dotnet/api/system.delegate.target#System_Delegate_Target) property and passed to the method automatically by CLR.
