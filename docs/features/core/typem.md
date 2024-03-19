TypeMap Data Type
====
[This proposal](https://github.com/dotnet/runtime/issues/59718) offers a data type that can be used to map the values to the generic argument. It's very similar to classic dictionary but the keys are generic types:
```csharp
public void Add<TKey>(TValue value);
public bool TryGetValue<TKey>(out TValue value);
```

This is useful when you want to build an association between the type and some value. At first look, the feature can be implemented using `Dictionary<Type, TValue>` where the keys are represented by the reflected types using **typeof** operator. But in practice, the dictionary demonstrates [lower performance](../../benchmarks.md).

As a response to the proposal, .NEXT provides the following features:
* [ITypeMap&lt;TValue&gt;](xref:DotNext.Collections.Specialized.ITypeMap`1) interface that provides a set of methods similar to [IDictionary&lt;TKey,TValue&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.idictionary-2) interface
* [TypeMap&lt;TValue&gt;](xref:DotNext.Collections.Specialized.TypeMap`1) is an implementation of `ITypeMap<TValue>` interface with very fast access to the values associated with generic arguments. However, this type is not thread-safe
* [ConcurrentTypeMap&lt;TValue&gt;](xref:DotNext.Collections.Specialized.ConcurrentTypeMap`1) is a thread-safe implementation of `ITypeMap<TValue>` interface with a convenient methods for concurrent access

Both implementations are highly optimized for fast access to the value associated with the types.

Additionally, the library provides set-like counterpart of this API:
* Non-generic [ITypeMap](xref:DotNext.Collections.Specialized.ITypeMap) interface that provides a set of methods similar to [ISet&lt;T&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.iset-1)
* Non-generic [TypeMap](xref:DotNext.Collections.Specialized.TypeMap) is an implementation of `ITypeMap` interface with very fast acces to the typed values
* Non-generic [ConcurrentTypeMap](xref:DotNext.Collections.Specialized.ConcurrentTypeMap) is a thread-safe implementation of `ITypeMap` interface with a convenient methods for concurrent access