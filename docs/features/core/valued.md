Value Delegates
====
Value Delegates provide general-purpose, CLS compliant, allocation-free, lightweight callback capability to .NET languages. They can be used as regular .NET delegates but have different properties:
1. Value Delegate is a value type and don't require heap allocation
1. Multicast is not supported
1. Value Delegate cannot be used for declaration of events
1. It is not possible to declare custom Value Delegate

.NEXT provides ready-to-use set of Value Delegates:
1. [ValueAction](../../api/DotNext.ValueAction.yml) as alternative to [Action](https://docs.microsoft.com/en-us/dotnet/api/system.action)
1. [ValueAction&lt;T&gt;](../../api/DotNext.ValueAction-1.yml) as alternative to [Action&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.action-1)
1. [ValueAction&lt;T1,T2&gt;](../../api/DotNext.ValueAction-2.yml) as alternative to [Action&lt;T1,T2&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.action-2)
1. [ValueAction&lt;T1,T2,T3&gt;](../../api/DotNext.ValueAction-3.yml) as alternative to [Action&lt;T1,T2,T3&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.action-3)
1. [ValueAction&lt;T1,T2,T3,T4&gt;](../../api/DotNext.ValueAction-4.yml) as alternative to [Action&lt;T1,T2,T3,T4&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.action-4)
1. [ValueAction&lt;T1,T2,T3,T4,T5&gt;](../../api/DotNext.ValueAction-5.yml) as alternative to [Action&lt;T1,T2,T3,T4&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.action-5)
1. [ValueFunc&lt;R&gt;](../../api/DotNext.ValueFunc-1.yml) as alternative to [Func&lt;R&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.func-1)
1. [ValueFunc&lt;T,R&gt;](../../api/DotNext.ValueFunc-2.yml) as alternative to [Func&lt;T,R&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.func-2)
1. [ValueFunc&lt;T1,T2,R&gt;](../../api/DotNext.ValueFunc-3.yml) as alternative to [Func&lt;T1,T2,R&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.func-3)
1. [ValueFunc&lt;T1,T2,T3,R&gt;](../../api/DotNext.ValueFunc-4.yml) as alternative to [Func&lt;T1,T2,T3,R&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.func-4)
1. [ValueFunc&lt;T1,T2,T3,T4,R&gt;](../../api/DotNext.ValueFunc-5.yml) as alternative to [Func&lt;T1,T2,T3,T4,R&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.func-5)
1. [ValueFunc&lt;T1,T2,T3,T4,T5,R&gt;](../../api/DotNext.ValueFunc-6.yml) as alternative to [Func&lt;T1,T2,T3,T4,T5,R&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.func-6)
1. [ValueRefAction&lt;T,TArgs&gt;](../../api/DotNext.ValueRefAction-2.yml) as alternative to [RefAction&lt;T,TArgs&gt;](../../api/DotNext.RefAction-2.yml)
1. [ValueRefFunc&lt;T,TArgs,TResult&gt;](../../api/DotNext.ValueRefFunc-3.yml) as alternative to [RefFunc&lt;T,TArgs,TResult&gt;](../../api/DotNext.RefFunc-3.yml)
1. [ValueSpanAction&lt;T,TArg&gt;](../../api/DotNext.ValueSpanAction-2.yml) as alternative to [SpanAction&lt;T,TArg&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.spanaction-2)
1. [ValueReadOnlySpanAction&lt;T,TArg&gt;](../../api/DotNext.ValueReadOnlySpanAction-2.yml) as alternative to [ReadOnlySpanAction&lt;T,TArg&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.readonlyspanaction-2)

> [!NOTE]
> It is recommended to pass Value Delegates by reference using **in** modifier to avoid copying on the stack. 

Value Delegates are convertible with regular delegates in both directions:
```csharp
using DotNext;
using System;

Func<string, int> parse = int.Parse;
//from regular to Value Delegate
ValueFunc<string, int> valueFunc = new ValueFunc<string, int>(parse);
//from Value Delegate to regular
parse = valueFunc.ToDelegate();
```

Internally, Value Delegate is operating in two modes:
1. _Normal_ mode means that the delegate just holds the pointer to the managed method for subsequent invocations.
1. _Proxy_ mode means that the delegate acts as a wrapper for regular .NET delegate.

However, _proxy_ mode is not useless and allow to achieve uniformity across API, utilizing both types of delegates:
```csharp
using DotNext;
using System;

public static class ArrayUtils
{
	public static void Sort<T>(T[] array, in ValueFunc<T, T, int> comparer)
	{
		//sorting algorithm
	}

	public static void Sort<T>(T[] array, Func<T, T, int> comparer)
		=> Sort(array, new ValueFunc<T, T, int>(comparer));
}
```
The mode depends on the constructor chosen for instantiation of a value delegate. All value delegates support two constructors:
* Accepting pointer to the managed method. When used, value delegate operates in _Normal_ mode
* Accepting delegate instance. When used, value delegate operates in _Proxy_ mode

The following example demonstrates how to wrap method pointer into value delegate:
```csharp
using DotNext;
using System;

static int CompareInt64(long x, long y)
{
	if(x < y)
		return -1;
	if(x > y)
		return 1;
	return 0;
}

var ComparerInt64 = new ValueFunc<long, long, int>(&CompareInt64);
```

> [!WARNING]
> It is not recommended to create Value Delegates using lambda expression because C# compiler produces hidden closure even if nothing is captured from outer lexical scope. This closure is stored in _Target_ property of the delegate and force _proxy_ mode.

Invocation of Value Delegate has approximately the same performance as regular .NET delegates. To verify that, check out [Benchmarks](../../benchmarks.md).

# Instance Methods
Capturing of instance non-abstract methods are not supported in _normal_ mode. However, the early prototype had such support but later it was dropped. The main reason is IL limitation: it is not possible to express **this** argument in a uniform way for both value and reference types. This magic is only allowed for virtual calls using `.constrained` prefix in combination with `callvirt` instruction but not for `calli` instruction. The second reason is C# compiler which allows to specify static method for open delegate or instance method for closed delegate. There is no syntax for open instance method. As a result, open delegates created for instance methods are used rarely.

# Dynamically Loaded Assemblies
The assembly can be loaded dynamically using [AssemblyLoadContext](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.loader.assemblyloadcontext), [AppDomain](https://docs.microsoft.com/en-us/dotnet/api/system.appdomain) or generated using [AssemblyBuilder](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.emit.assemblybuilder). The loaded assembly can be unloaded and pointer to the method declared in such assembly becomes invalid. This problem is called _dangling pointer_ which is well known in C/C++ world. The problem is only observed in _Normal_ mode of Value Delegate because this mode operates with method pointer directly.

As a result, it's not recommended to use value delegates in programs relying on dynamic loading of assemblies.
