Optional Type
====
[Optional](../../api/DotNext.Optional-1.yml) is a container which may or may not contain a value. [Nullable&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.nullable-1) type can work with value types only. `Optional<T>` data type can work with reference and value type both.

The following example demonstrates usage of this type:
```csharp
using DotNext;
using DotNext.Collections.Generic;

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

# Null vs Undefined
Let's take a look at the following code:
```csharp
using DotNext;
using System;

static Optional<string> FirstOrNull(string[] array)
    => array.Length > 0 ? array[0] : Optional<string>.Empty;

string[] array1 = { null };
Optional<string> first1 = FirstOrNull(array1);

string[] array2 = Array.Empty<string>();
Optional<string> first2 = FirstOrNull(array2);
```

`HasValue` property of both values `first1` and `first2` is **false**. However, `first1` actually represents the first element from the array. But the element is **null**. `first2` is empty because the array is empty. This situation is equivalent to the following code:
```csharp
using DotNext;

var first1 = new Optional<string>(null);
var first2 = Optional<string>.Empty;    //or default(Optional<string>)
```

Is it possible to distinguish the absence of value from **null** value? The answer is yes. There are two additional properties:
* `IsNull` returns **true** if underlying value is **null**
* `IsUndefined` returns **true** if underlying value is not defined

Now it's possible to apply additional logic to the optional result:
```csharp
Optional<string> first = FirstOrNull(array);

if (first.HasValue)
{
    // value is present
    string result = first.Value;
}
else if (first.IsNull)
{
    // value is null
}
else
{
    // result is undefined
}
```

Undefined `Optional<T>` instance can be produced only by `Empty` static property or by default value:
```csharp
using DotNext;

Optional<string>.Empty; // IsUndefined == true, IsNull == false
new Optional<string>(); // IsUndefined == true, IsNull == false
default(Optional<string>);  // IsUndefined == true, IsNull == false
new Optional<string>(null); // IsUndefined == false, IsNull == true
```