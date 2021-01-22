Enum Enhancements
====
[EnumConverter](../../api/DotNext.EnumConverter.yml) allows to convert primitive types into arbitrary enum type and vice versa. It is helpful if enum type is defined as generic parameter and not known at compile time.

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

# Flag
[System.Enum](https://docs.microsoft.com/en-us/dotnet/api/system.enum) class has public instance method `HasFlag` that allows to check whether the one or more bits are set in the enumeration. However, it causes boxing of the argument. There is no generic version of this method in standard library to avoid boxing.

.NEXT library offers fast version of `HasFlag` method that prevents boxing. It's represented by static method located in [Intrinsics](https://sakno.github.io/dotNext/api/DotNext.Runtime.Intrinsics.html) class.

# Attributes
[EnumType](https://sakno.github.io/dotNext/api/DotNext.Reflection.EnumType.html) static classes exposes access to custom attributes attached to the enum member.

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