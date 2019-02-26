Optional Type
====
[Optional](../../api/DotNext.Optional-1.yml) data type is a rich version of nullable container shipped with .NET Standard. `Nullable<T>` type can work only with value types. `Optional<T>` data type can work with reference and value type both. Moreover, the underlying type can implement optional interface [IOptional](../../api/DotNext.IOptional.yml) to indicate whether the object doesn't have meaningful content, event object is not null.

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
if(i)
{
    value = i.Value;
}
value = i.OrThrow<ArgumentException>();
value = i.Or(-1);   //returns -1 if i has no value
value = i.OrDefault(); //returns default(int) if i has no value
value = i.OrInvoke(() => 10); //calls lambda if u has no value
```
