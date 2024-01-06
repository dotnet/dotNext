Value Type Enhancements
====
It's a challenge to implement _Equals_ and _GetHashCode_ methods for custom value type. Moreover, third-party library may provide value type without these methods and without overloaded equality/inequality operators. As a result, there is no way to identify whether the struct value is the default value.

.NEXT library provides generic methods for equality check and hash code calculation for any value type. These methods are located in [BitwiseComparer](xref:DotNext.BitwiseComparer`1) and [Intrinsics](xref:DotNext.Runtime.Intrinsics) classes and not extension methods to avoid ambiguity with overridden methods.
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