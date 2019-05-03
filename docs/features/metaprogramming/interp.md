String Interpolation
====
String Interpolation Expression allows to simplify code generation associated with string formatting. This feature is powered by string interpolation in [C#](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/tokens/interpolated) and [VB.NET](https://docs.microsoft.com/en-us/dotnet/visual-basic/programming-guide/language-features/strings/interpolated-strings).

In Metaprogramming library, interpolated string expression is represented by [InterpolationExpression](../../api/DotNext.Linq.Expressions.InterpolationExpression.yml) class.

```csharp
using DotNext.Linq.Expressions;
using static DotNext.Linq.Expressions.InterpolationExpression;
using static DotNext.Metaprogramming.CodeGenerator;

Lambda<Action<int, int>>(fun =>
{
	var (x, y) = fun;
	WriteLine(PlainString($"Sum of {x} and {y} is {x.Add(y)}"));
});

//generated code is

new Action<int, int>(x, y => Console.WriteLine($"Sum of {x} and {y} is {x + y}"));
```

`PlainString` factory method converts interpolated string into expression node of type [String](https://docs.microsoft.com/en-us/dotnet/api/system.string). So, at runtime, it will be represented as formatted string.

`FormattableString` factory method converts interpolated string into expression node of type [FormattableString](https://docs.microsoft.com/en-us/dotnet/api/system.formattablestring). It can be helpful to change the culture of the string at runtime