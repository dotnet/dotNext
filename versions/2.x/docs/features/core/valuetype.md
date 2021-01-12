Value Type Enhancements
====
It's a challenge to implement _Equals_ and _GetHashCode_ methods for custom value type. Moreover, third-party library may provide value type without these methods and without overloaded equality/inequality operators. As a result, there is no way to identify whether the struct value is the default value.

.NEXT library provides generic methods for equality check and hash code calculation for any value type. These methods are located in [BitwiseComparer](../../api/DotNext.BitwiseComparer-1.yml) and [Intrinsics](../../api/DotNext.Runtime.Intrinsics.yml) classes and not extension methods to avoid ambiguity with overridden methods.
```csharp
using DotNext;
using System;

var guid = Guid.NewGuid();
BitwiseComparer<Guid>.Equals(guid, new Guid());    //false
BitwiseComparer<Guid>.GetHashCode(guid);
Intrinsics.IsDefault(guid);    //false
Intrinsics.IsDefault(new Guid());  //true
BitwiseComparer<Guid>.Compare(guid, new Guid());   // greater than 0
```

These methods use bitwise representation of struct in memory, not reflection tricks. Therefore, they are optimized for the best performance. Especially, for value types with many fields bitwise equality can demonstrate better performance in comparison with field-by-field comparison typically used for custom _Equals_ implementation. Therefore, bitwise equality and hash code computation can be used in custom implementation of _Equals_ and _GetHashCode_ to save the time and the best performance.

```csharp
using DotNext;
using System;

public struct MyStruct: IEquatable<MyStruct>
{
    private int fieldA;
    private int fieldB;
    private int fieldC;

    public bool Equals(MyStruct other) => BitwiseComparer<MyStruct>.Equals(this, other);

    public override bool Equals(object other) => other is MyStruct typed && Equals(typed);

    public override int GetHashCode() => BitwiseComparer<MyStruct>.GetHashCode(this);
}
```

Additionally, bitwise hash code computation method may accept custom hash code algorithm. Check API documentation for more information.

# Fast bitwise cast between value types
The library provides fast way to convert one unmanaged value type into another even if value type doesn't provide custom implicit or explicit type cast operator. This logic is provided by extension method `Bitcast`. Bitwise cast can be performed between two value types of different size.
```csharp
using DotNext.Runtime;

Intrinsics.Bitcast(20, out bool value); //value is true
Intrinsics.Bitcast(0, out bool value);  //value is false
```

The following example demonstrates how to extract content of the _Guid_ data type.
```csharp
using DotNext.Runtime;

internal struct GuidRawData //Guid size is 16 bytes
{
    internal ulong Component1;
    internal ulong Component2;
}

Intrinsics.Bitcast(Guid.NewGuid, out GuidRawData data);
data.Component1 = 0;
Intrinsics.Bitcast(data, out Guid guid);    //convert back to Guid
```

`Bitcast` provides bitwise copy of the original structure so it is very fast.