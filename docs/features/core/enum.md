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

[EnumConverter](../../api/DotNext.Enum-1.yml) allows to convert primitive types into arbitrary enum type and vice versa. It is helpful if enum type is defined as generic parameter and not known at compile time.

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