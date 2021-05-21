Optional Type
====
[Optional&lt;T&gt;](xref:DotNext.Optional`1) is a container which may or may not contain a value. [Nullable&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.nullable-1) type can work with value types only. `Optional<T>` data type can work with reference and value type both.

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

`Nullable<T>` and `Optional<T>` are mutually convertible types with help of [extension methods](xref:DotNext.Optional).

# Null vs Undefined
Let's take a look at the following code:
```csharp
using DotNext;
using System;

static Optional<string> FirstOrNull(string[] array)
    => array.Length > 0 ? array[0] : Optional<string>.None;

string[] array1 = { null };
Optional<string> first1 = FirstOrNull(array1);

string[] array2 = Array.Empty<string>();
Optional<string> first2 = FirstOrNull(array2);
```

`HasValue` property of both values `first1` and `first2` is **false**. However, `first1` actually represents the first element from the array. But the element is **null**. `first2` is empty because the array is empty. This situation is equivalent to the following code:
```csharp
using DotNext;

var first1 = new Optional<string>(null);
var first2 = Optional<string>.None;    //or default(Optional<string>)
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

Undefined `Optional<T>` instance can be produced only by `None` static property or by default value:
```csharp
using DotNext;

Optional<string>.None; // IsUndefined == true, IsNull == false
new Optional<string>(); // IsUndefined == true, IsNull == false
default(Optional<string>);  // IsUndefined == true, IsNull == false
new Optional<string>(null); // IsUndefined == false, IsNull == true
```

There is also convenient factory methods for creating optional values:
```csharp
using DotNext;

Optional<string> nullValue = Optional.Null<string>();       // undefined
Optional<string> undefinedValue = Optional.None<string>();  // null
Optional<string> value = Optional.Some("Hello, world!");    // not null
```

Behavior of `Equals` method and equality operators depend on underlying value and its presence. Undefined and null values behave similarily to JavaScript.
```csharp
using DotNext;

Optional.Null<string>() == Optional.Null<string>(); // true
Optional.None<string>() == Optional.None<string>(); // true
Optional.None<string>() == Optional.Null<string>(); // false
```

> [!IMPORTANT]
> Prior to .NEXT 3.2.0, null value is equal to undefined value, i.e. `Optional.None<string>() == Optional.Null<string>()` evaluated as true. Starting from 3.2.0 the behavior is changed. If you need to return previous behavior, then enable _DotNext.Optional.UndefinedEqualsNull_ switch using `SetSwitch` method from [AppContext](https://docs.microsoft.com/en-us/dotnet/api/system.appcontext) class.

# JSON serialization
[Optional&lt;T&gt;](xref:DotNext.Optional`1) is compatible with JSON serialization provided by `System.Text.Json` namespace. You can enable support of this type using [OptionalConverterFactory](xref:DotNext.Text.Json.OptionalConverterFactory) converter. The converter can be applied to the property or field directly using [JsonConverterAttribute](https://docs.microsoft.com/en-us/dotnet/api/system.text.json.serialization.jsonconverterattribute) attribute or it can be registered via [Converters](https://docs.microsoft.com/en-us/dotnet/api/system.text.json.jsonserializeroptions.converters) property.

If the value of `Optional<T>` is undefined, then property will be completely removed from serialized JSON document. This is useful when you want to describe Data Transfer Object for your resource in REST API that allows partial updates with _PATCH_ HTTP method. To make this magic work, you also need to apply [JsonIgnoreAttribute](https://docs.microsoft.com/en-us/dotnet/api/system.text.json.serialization.jsonignoreattribute) with condition equal to [JsonIgnoreCondition.WhenWritingDefault](https://docs.microsoft.com/en-us/dotnet/api/system.text.json.serialization.jsonignorecondition). This condition tells JSON serializer to drop the field if its value is equal to default value. As we know, the default value of `Optional<T>` is always undefined. The following example demonstrates how to design DTO with optional JSON fields:
```csharp
using DotNext.Text.Json;
using System.Text.Json;

public sealed class JsonObject
{
    [JsonConverter(typeof(OptionalConverterFactory))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<int> IntegerValue { get; set; } // optional field

    [JsonConverter(typeof(OptionalConverterFactory))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<string> StringField { get; set; } // optional field

    public bool BoolField{ get; set; } // required field which is always presented in JSON
}
```