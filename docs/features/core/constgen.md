Generic specialization with constant values
====
Such kind of specialization [widely used](https://en.cppreference.com/w/cpp/language/template_parameters) in C++ to pass a constant known at compile time as template argument. C# and Common Language Runtime do not support this feature directly. DotNext library offers such functionality via conversion of actual generic argument into value using implicit cast conversion. This approach doesn't provide compile time check of constant value passed as generic argument but the type of this constant is controlled by compiler.

The base class for any constant-value-as-generic-argument is [Constant](../../api/DotNext.Generic.Constant-1.yml). This type itself is a generic class which type parameter indicates type of the constant value. If you need to pass constant value as generic argument then two restrictions should be satisfied:
1. Generic parameter should be of type **Constant**
1. Generic parameter should have constructor without parameters

The following example demonstrates how to declare custom array type with the specific length passed as constant value into generic argument:
```csharp
using DotNext.Generic;

public sealed class IntVector<SIZE> where SIZE: Constant<long>, new()
{
    private static readonly long Size = new SIZE();

    private readonly int[] array = new int[Size];
}

//declare constant as type
internal sealed class Ten: Constant<long>
{
    public Ten(): base(10) { }
}

//instantiate vector
var vector = new IntVector<Ten>();  //creates vector of length 10
```

Constant value passed as generic argument is available in static context inside of vector class. Such approach allows to pass custom data into static constructor and avoid passing unecessary data through instance constructor.

The library offers a few predefined constant types:
* [BooleanConst](../../api/DotNext.Generic.BooleanConst.yml) to pass boolean constant as generic argument
* [Int32Const](../../api/DotNext.Generic.Int32Const.yml) to pass integer constant as generic argument
* [Int64Const](../../api/DotNext.Generic.Int64Const.yml) to pass long integer constant as generic argument
* [StringConst](../../api/DotNext.Generic.StringConst.yml) to pass string constant as generic argument
* [DefaultConst](../../api/DotNext.Generic.DefaultConst-1.yml) to pass default value of `T` as generic argument