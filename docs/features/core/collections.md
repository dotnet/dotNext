Collection Enhancements
====
Collection Helpers are set of extension methods and classes aimed to improve _System.Linq.Enumerable_ and .NET Collection types:

# Read-only mapped view
Transformation of collection types can be done in lazy mode when item is converting on-demand without touching entire collection. Lazy converted collection is called _mapped view_.

There are several mapped views for different collection types:
* [Read-only list view](../../api/DotNext.Collections.Generic.ReadOnlyListView-2.yml) for lists and arrays
* [Read-only collection view](../../api/DotNext.Collections.Generic.ReadOnlyCollectionView-2.yml) for generic collections without indexer support
* [Read-only dictionary view](../../api/DotNext.Collections.Generic.ReadOnlyDictionaryView-3.yml) for generic dictionaries

The following example demonstrates how to obtain read-only mapped view for the list:
```csharp
using DotNext.Collections.Generic;
using System.Collections.Generic;

var list = new List<string>() { "1", "2", "3" };
var mappedList = list.Convert(int.Parse);
var first = mappedList[0];	//first == 1
```

# ToArray
Extension method _ToArray_ allows to convert arbitrary collection into array. Collection should implements `ICollection<T>` or `IReadOnlyCollection<T>` interface.

```csharp
using System.Collections.Generic;
using DotNext.Collections.Generic;

ICollection<string> collection = new string[] { "a", "b", "c" };
var array = collection.ToArray();   //array = new [] {"a", "b", "c" }
```

# Singleton list
The library provides optimized version of the list with the single element in it.

```csharp
using DotNext.Collections.Generic;

IReadOnlyList<string> list = List.Singleton("a");
```

# Stack helper methods
The library provides safe versions of _Peek_ and _Pop_ methods which do not throw exceptions:

```csharp
using System.Collections.Generic;
using DotNext.Collections.Generic;

var stack = new Stack<int>();
if(stack.TryPeek(out var top))
{

}
if(stack.TryPop(out var top))
{

}
```

Additionally, there is a simple way to create a copy of the stack respecting order of the elements:
```csharp
using System.Collections.Generic;
using DotNext.Collections.Generic;

var stack = new Stack<int>();
stack = Stack.Clone(stack);
```

# Collection items concatenation
Extension method _ToString_ from class [Sequence](../../api/DotNext.Sequence.yml) allows to convert collection items into single plain string. Each item is separated by the specified delimiter.

```csharp
using DotNext;

var array = new int[] {1, 2, 3};
var str = array.ToString(",");  //str is 1,2,3
```

# Iteration
Iteration in functional style is possible using extension method _ForEach_ which is applicable to any type implementing interface `IEnumerable<T>`.

```csharp
using System;
using DotNext;

IEnumerable<string> list = new[] { "a", "b", "c" };
list.ForEach(item => Console.WriteLine(item));
```

# CopyOnWriteList
[CopyOnWriteList](../../api/DotNext.Collections.Concurrent.CopyOnWriteList-1.yml) is a thread-safe variant of [List](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.list-1) in which all mutative operations (except element setter) are implemented by making a fresh copy of the underlying array.

This is ordinarily too costly, but may be more efficient than alternatives when traversal operations vastly outnumber mutations, and is useful when you cannot or don't want to synchronize traversals, yet need to preclude interference among concurrent threads. The "snapshot" style enumerator method uses a reference to the state of the array at the point that the enumerator was created. The size of array never changes during the lifetime of the enumerator, so interference is impossible and the enumerator is guaranteed not to throw [InvalidOperationException](https://docs.microsoft.com/en-us/dotnet/api/system.invalidoperationexception). The enumerator will not reflect additions or removals to/from the list since the eumerator was created.

All elements are permitted, including **null**.