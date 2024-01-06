Enum Enhancements
====
[EnumConverter](xref:DotNext.EnumConverter) allows to convert primitive types into arbitrary enum type and vice versa. It is helpful if enum type is defined as generic parameter and not known at compile time.

```csharp
using DotNext;
using System;

enum Color: int
{
    Black = 0,
    Red = 0xFF0000,
    Green = 0x00FF00,
    Blue = 0x0000FF,
    White = 0xFFFFFF
}

public static E Plus<E>(E first, E second, E third) 
    where E: unmanaged, Enum
{
    var result = first.ToInt64() + second.ToInt64() + third.ToInt64();
    return result.ToEnum<E>();
}

var white = Plus(Color.Red, Color.Green, Color.Blue);
white == Color.White;   //true
```

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