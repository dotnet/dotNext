Enum Enhancements
====
Currently, to retrieve both the names and values of an enum's members requires two separate calls and requires you to use a for loop which is quite clumsy. Moreover, static methods from [Enum](https://docs.microsoft.com/en-us/dotnet/api/system.enum) to work with arbitrary enum types cause boxing/unboxing. [Enum&lt;T&gt;](../../api/DotNext.Enum-1.yml) allows to obtain value and name of the enum member as well as to obtain the member by its value or name.

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

Enum<Color>.Member member = Enum<Color>.GetMember("Red");
member.Value == Color.Red;  //true
member = Enum<Color>.GetMember(Color.Green);
member.Name == "Green"; //true
member = default;
member.Name == "Black"; //true
member.Value == Color.Black;    //true
```

It is possible to enumerate all members in the order they were declared. Moreover, it is possible to obtain the member by its index

```csharp
foreach(var member in Enum<Color>.Members)
    Console.WriteLine(member.Name);

Enum<Color>.Member red = Enum<Color>.Members[1];
```

[EnumConverter](../../api/DotNext.EnumConverter.yml) allows to convert primitive types into arbitrary enum type and vice versa. It is helpful if enum type is defined as generic parameter and not known at compile time.

```csharp
using DotNext;
using System;

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

.NEXT library offers generic and fast version of `HasFlag` method that prevents boxing:
* Instance method `IsFlag` of [Enum&lt;T&gt;](https://sakno.github.io/dotNext/api/DotNext.Enum-1.html) value type
* Static method `HasFlag` of [Intrinsics](https://sakno.github.io/dotNext/api/DotNext.Runtime.Intrinsics.html) class

# Attributes
[Enum&lt;T&gt;](https://sakno.github.io/dotNext/api/DotNext.Enum-1.html) value type exposes access to custom attributes attached to the enum member. The attributes can be requested using methods of [ICustomAttributeProvider](https://docs.microsoft.com/en-us/dotnet/api/system.reflection.icustomattributeprovider) interface which is implemented by the value type. For convenience, there are extra methods available with the support of generic parameters:

```csharp
using DotNext;

sealed class EnumMemberAttribute : Attribute
{
}

enum MyEnum
{
    None = 0,

    [EnumMember]
    WithAttribute = 1,
}

Enum<MyEnum> enumMember = Enum<MyEnum>.GetMember(MyEnum.WithAttribute);
EnumMemberAttribute attr = enumMember.GetCustomAttribute<EnumMemberAttribute>();
```