---
name: dotnext-core
description: Core types from the DotNext NuGet package - Optional<T> and Result<T> monads,
  Optional JSON serialization (PATCH DTOs with omitted fields), Timestamp, EnumType attribute lookup,
  ConsoleLifetimeTokenSource, TypeMap/ConcurrentTypeMap, LocalReference and UserData.
  Use when expressing absent values or errors without exceptions; when distinguishing undefined and null
  JSON fields; when measuring elapsed time or tracking deadlines; when reading attributes of enum members;
  when handling console app shutdown (Ctrl+C, SIGTERM) without a generic host; when a dictionary is keyed
  by System.Type; when a ref field to a ref struct is needed; or when attaching data to objects
  via ConditionalWeakTable.
license: MIT
metadata:
  dotnext-version: "6.x"
---

# Before you start

1. Check which version of `DotNext` the project references (`.csproj` or
   `Directory.Packages.props`). This skill describes 6.x. For older versions, verify each
   member you use against the XML docs/IntelliSense of the referenced version.
2. The types below are in several namespaces. Each type is given with its namespace
   (e.g. `DotNext.Diagnostics.Timestamp`). Add the `using` for that namespace and use the short type name in code.
3. If this skill doesn't cover what you need (API details, more examples), find the relevant article
   in the documentation index: https://dotnet.github.io/dotNext/llms.txt

# Use Cases

## Monadic Types
- Consider `DotNext.Optional<T>` monad to express value absence in a unified way for both reference and value types.
  - This type is compatible with `System.Text.Json` serialization via `DotNext.Text.Json.OptionalConverterFactory` converter. Use `DotNext.Text.Json.OptionalConverter<T>` explicitly for AOT and self-contained deployment.
  - To omit the field from JSON when the value is undefined (equivalent to **undefined** in JavaScript, useful for PATCH DTOs), the property needs **both** attributes. The default value of `Optional<T>` is undefined, so `WhenWritingDefault` drops it. Without `JsonIgnore`, the converter throws `InvalidOperationException` for undefined value.
    ```csharp
    [JsonConverter(typeof(OptionalConverterFactory))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<int> IntegerValue { get; set; }
    ```
  - Optional with **null** value (`IsNull`) is not undefined. It is written as JSON `null`.
  - `AsyncEnumerable.Flatten` and `Collection.Flatten` in `DotNext.Collections.Generic` extension methods can be used to filter out the empty values in a sequence.
- Consider `DotNext.Result<T>` monad to express error or successful result as a return type in synchronous methods
  - Avoid `Task<Result<T>>` or `Result<Task<T>>` wrapping. `Task<T>` in asynchronous code provides the same semantics: it can represent error or task result on success

## Diagnostics
- Consider `DotNext.Diagnostics.Timestamp` value type for time measurements instead of `System.Diagnostics.Stopwatch` class or raw `long` values returned by `Stopwatch.GetTimestamp()`.
  - It can be stored in a field and passed around: `Elapsed`, `ElapsedMilliseconds` and `ElapsedTicks` give the time passed since the timestamp was taken.
  - It supports comparison operators, and `timestamp + timeout` produces a deadline that can be checked with `IsPast` or `IsFuture`.
  - The constructor and elapsed time methods (`GetElapsedTime`, `GetElapsedTicks`, `GetElapsedMilliseconds`) have overloads that accept `System.TimeProvider`, so the code can be tested with a fake time provider. `Elapsed*`, `IsPast` and `IsFuture` properties always use the system clock.

## Reflection
- Consider `DotNext.Reflection.EnumType` extensions to obtain a custom attribute for the enum field. This approach is AOT compatible.

## Console Application
- If the app doesn't use the generic host (`Microsoft.Extensions.Hosting`), `DotNext.Hosting.ConsoleLifetimeTokenSource` can be used to get cancellation token associated with the console application lifetime. It gets canceled on `SIGINT` (`Ctrl+C`), `SIGQUIT` or `SIGTERM` signal (`SIGTERM` is sent by `docker stop` and Kubernetes).
  - Not supported on Android, iOS, tvOS and browser. Check `ConsoleLifetimeTokenSource.IsSupported` in cross-platform code.
  - Declare it with `using`. `Dispose` removes the signal handlers.

## Collections
- Consider `DotNext.Collections.Specialized.TypeMap<TValue>` or `DotNext.Collections.Specialized.ConcurrentTypeMap<TValue>` classes to replace `System.Collections.Generic.Dictionary<System.Type, TValue>` when keys are known at compile time. These specialized maps provide O(1) lookup.
  - The key is a generic type argument, e.g. `map.Set<string>(value)`, `map.TryGetValue<string>(out var value)`. A `System.Type` instance obtained at runtime cannot be used as a key.
  - `TypeMap<TValue>` is not thread-safe. Use `ConcurrentTypeMap<TValue>` for concurrent access.

## Misc
- Use `DotNext.LocalReference<T>` or `DotNext.ReadOnlyLocalReference<T>` if you need a **ref** field pointing to a **ref struct** (C# doesn't allow **ref** fields of **ref struct** type), or you need to represent managed pointer in generic context.
- Use `DotNext.BasicExtensions.UserData` extension property to attach arbitrary values to the objects instead of maintaining `System.Runtime.CompilerServices.ConditionalWeakTable<TKey, TValue>` manually.