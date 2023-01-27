Advanced Reflection
====
.NEXT provides additional methods to reflect collection types, delegates and task types. These methods are located in `DotNext.Reflection` namespace.

> [!IMPORTANT]
> `DotNext.Reflection` library doesn't receive new features anymore and will be deprecated soon. See [this post](https://github.com/dotnet/dotNext/discussions/142) for more information.

# Collection
.NET Reflection contains a [method](https://docs.microsoft.com/en-us/dotnet/api/system.type.getelementtype) to obtain type of elements in the array. .NEXT provides special class [CollectionType](xref:DotNext.Reflection.CollectionType) to reflect type of collection elements.
```csharp
using DotNext.Reflection;

var itemType = typeof(List<string>).GetItemType();  //itemType == typeof(string)
```

This method ables to extract item type from any class implementing [IEnumerable&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.ienumerable-1), even if such class is not generic class itself.

# Dispose pattern
[DisposableType](xref:DotNext.Reflection.DisposableType) allows to reflect `void Dispose()` method from any type. The reflection checks whether the class implements [IDisposable](https://docs.microsoft.com/en-us/dotnet/api/system.idisposable) method. If not, then it looks for the public instance parameterless method `Dispose` with **void** return type.

```csharp
using DotNext.Reflection;

var dispose = typeof(Stream).GetDisposeMethod();
```

# Tasks
[TaskType](xref:DotNext.Reflection.TaskType) provides a way to obtain actual generic argument of [Task](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.task-1). 
```csharp
using DotNext.Reflection;

var t = typeof(Task<int>).GetTaskType();    //t == typeof(int)
t = typeof(Task);   //t == typeof(void)
```

Additionally, it is possible to instantiate task type at runtime:
```csharp
using DotNext.Reflection;

var t = typeof(Task<>).MakeTaskType(typeof(string));    //t == typeof(Task<string>)
```