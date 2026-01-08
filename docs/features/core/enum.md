Enum Enhancements
====
[Enum&lt;T&gt;](xref:DotNext.Numerics.Enum`1) represents the enum as its underlying numeric type.

```csharp
using DotNext.Numerics;
using System;

enum Color: int
{
    Black = 0,
    Red = 0xFF0000,
    Green = 0x00FF00,
    Blue = 0x0000FF,
    White = 0xFFFFFF
}

Color e = new Enum<Color>(Color.Red) | new Enum<Color>(Color.Green) | new Enum<Color>(Color.Blue);
```

The type implements [IBinaryInteger&lt;T&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.numerics.ibinaryinteger-1) interface to expose the full power of the Generic Math.

# Attributes
[EnumType](xref:DotNext.Reflection.EnumType) static classes exposes access to custom attributes attached to the enum member.

```csharp
using DotNext.Reflection;

sealed class EnumMemberAttribute : Attribute
{
}

enum MyEnum
{
    None = 0,

    [EnumMember]
    WithAttribute = 1,
}

EnumMemberAttribute attr = MyEnum.WithAttribute.GetCustomAttribute<MyEnum, EnumMemberAttribute>();
```