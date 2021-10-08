String Interpolation
====
String Interpolation Expression allows to simplify code generation associated with string formatting. This feature is powered by string interpolation in [C#](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/tokens/interpolated) and [VB.NET](https://docs.microsoft.com/en-us/dotnet/visual-basic/programming-guide/language-features/strings/interpolated-strings).

In Metaprogramming library, interpolated string expression is represented by [InterpolationExpression](xref:DotNext.Linq.Expressions.InterpolationExpression) class.

```csharp
using DotNext.Linq.Expressions;
using static DotNext.Metaprogramming.CodeGenerator;

Lambda<Action<int, int>>(fun =>
{
	var (x, y) = fun;
	WriteLine(InterpolationExpression.PlainString($"Sum of {x} and {y} is {x.Add(y)}"));
});

//generated code is

new Action<int, int>(x, y => Console.WriteLine($"Sum of {x} and {y} is {x + y}"));
```

`PlainString` factory method converts interpolated string into expression node of type [String](https://docs.microsoft.com/en-us/dotnet/api/system.string). So, at runtime, it will be represented as a formatted string. Under the hood, the generated expression is a call to [String.Format](https://docs.microsoft.com/en-us/dotnet/api/system.string.format#System_String_Format_System_IFormatProvider_System_String_System_Object___) method.

`FormattableString` factory method converts interpolated string into expression node of type [FormattableString](https://docs.microsoft.com/en-us/dotnet/api/system.formattablestring). It can be helpful to change the culture of the string at runtime.

`Create` factory method converts interpolated string to a series of statements according to the transformation described in [this article](https://devblogs.microsoft.com/dotnet/string-interpolation-in-c-10-and-net-6/). This is the recommended way to express the interpolated string because it provides the best code generation quality. The generated code uses stack allocation and memory pooling to achieve zero heap allocation.