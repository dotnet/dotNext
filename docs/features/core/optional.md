Optional Type
====
[Optional](../../api/DotNext.Optional-1.yml) is a container which may or may not contain a value. [Nullable&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.nullable-1) type can work with value types only. `Optional<T>` data type can work with reference and value type both.

The following example demonstrates usage of this type:
```csharp
using DotNext;

IEnumerable<int> array = new int[] { 1, 2, 3 };
Optional<int> i = array.FirstOrEmpty(); //extension method from Sequence class
var value = (int)i; //cast is supported
if(i.TryGet(out value))
{
    //if i has value
}
if(i)   //if i has value
{
    value = i.Value;
}
value = i.OrThrow<ArgumentException>();
value = i.Or(-1);   //returns -1 if i has no value
value = i.OrDefault(); //returns default(int) if i has no value
value = i.OrInvoke(() => 10); //calls lambda if i has no value
```

`Nullable<T>` and `Optional<T>` are mutually convertible types with help of [extension methods](../../api/DotNext.Optional.yml).