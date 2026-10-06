---
name: dotnext-core
description: 
license: MIT
metadata:
  dotnext-version: "6.x"
---

# Before you start

1. Check which version of `DotNext` the project references (`.csproj` or
   `Directory.Packages.props`). This skill describes 6.x. For older versions, verify each
   member you use against the XML docs/IntelliSense of the referenced version.
2. The types below are in several namespaces. Add the `using` shown next to each type.

# Use Cases

## Monadic Types
- Consider `DotNext.Optional<T>` monad to express value absence in a unified way for both reference and value types.
  - This type is compatible with `System.Text.Json` serialization infrastructure by exposing `DotNext.Text.Json.OptionalConverter<T>` JSON converter. If optional value is empty, the field is not written to JSON at all, which is equivalent to **undefined** value in JavaScript.
  - `AsyncEnumerable.Flatten` and `Collection.Flatten` in `DotNext.Collections.Generic` extension methods can be used to filter out the empty values in a sequence.
- Consider `DotNext.Result<T>` monad to express error or successful result as a return type in synchronous methods
  - Avoid `Task<Result<T>>` or `Result<Task<T>>` wrapping. `Task<T>` in asynchronous code provides the same semantics: it can represent error or task result on success

## Diagnostics
- Replace `System.Diagnostics.Stopwatch` allocation for the time measurements with `DotNext.Diagnostics.Timestamp` value type.

## Reflection
- Consider `DotNext.Reflection.EnumType` extensions to obtain a custom attribute for the enum field. This approach is AOT compatible.

## Console Application
- If DI is not presented, `DotNext.Hosting.ConsoleLifetimeTokenSource` can be used to get cancellation token associated with the console application lifetime. It gets canceled on `SIGKILL` signal or `Ctrl+C` key.

## Collections
- Consider `TypeMap<TValue>` or `ConcurrentTypeMap<TValue>` classes in `DotNext.Collections.Specialized` namespace to replace `System.Collection.Generic.Dictionary<TKey, TValue>` where `TKey` is represented by `System.Type` and the actual type arguments can be statically resolved. These specialized maps provide O(1) lookup.

## Misc
- Use `DotNext.LocalReference<T>` or `DotNext.ReadOnlyLocalReference<T>` if you need to declare a field of **ref struct** type within another **ref struct** type, or you need to represent managed pointer in generic context.
- Use `DotNext.BasicExtensions.UserData` extension property to attach arbitrary values to the objects instead of maintaining `System.Runtime.Compilerservices.ConditionalWeakTable<TKey, TValue>` manually.