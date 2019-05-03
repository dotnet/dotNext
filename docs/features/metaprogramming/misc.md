Miscellaneous Statements
====

# WriteLine Expression
[WriteLineExpression](../../api/DotNext.Linq.Expressions.WriteLineExpression.yml) can be used to write line of the text in dynamically generated code. This expression supports several outputs for the text:
* [Standard Output Stream](https://docs.microsoft.com/en-us/dotnet/api/system.console.out)
* [Standard Error Stream](https://docs.microsoft.com/en-us/dotnet/api/system.console.error)
* [Debug Output](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.debug.writeline#System_Diagnostics_Debug_WriteLine_System_Object_)

Additionally, Code Generator has static methods `WriteLine`, `WriteError` and `DebugMessage` that allow to place this expression as statement into lexical scope.

```csharp
using System;
using static DotNext.Metaprogramming.CodeGenerator;

var println = Lambda<Action<string>>(fun =>
{
	WriteLine(fun[0]);
});
```

# Assertion
Checks for a condition; if the condition is false, outputs messages and displays a message box that shows the call stack. This statement is available as static method `Assert` from Code Generator only.

The statement is relevant in DEBUG configuration only. In any other configurations, `Assert` method is ignored.

```csharp
using DotNext.Linq.Expressions;
using System;
using static DotNext.Metaprogramming.CodeGenerator;

var println = Lambda<Action<string>>(fun =>
{
	Assert(fun.IsNotNull(), "Argument is null");
	WriteLine(fun[0]);
});
```

# Breakpoint
Signals a breakpoint to an attached debugger. This statement is available as static method `Breakpoint` from Code Generator only.

The statement is relevant in DEBUG configuration only. In any other configurations, `Breakpoint` method is ignored.

```csharp
using DotNext.Linq.Expressions;
using System;
using static DotNext.Metaprogramming.CodeGenerator;

var println = Lambda<Action<string>>(fun =>
{
	Breakpoint();
	WriteLine(fun[0]);
});
```

# Fragment
Regardless for rich set of helper methods for generating statements and expressions the code written for dynamic code generation may hard to read by developers. It reasonable to simplify construction of compound expressions or statements somehow. C# programming language supports creation of expression tree from single-line expression. This feature is utilized by Metaprogramming library and called **expression fragment**. The fragment is a body of lambda expression with parameters replaced by actual expressions from the context. It can be embedded as statement inside of multi-line lambda expression.

The following example demonstrates how to generate expression fragment:
```csharp
using DotNext.Linq.Expressions;
using System;

var fragment = ExpressionBuilder.Fragment<Func<int, int, int>>((x, y) => Math.Max(x, y), 10, 20);
//fragment is MethodCallExpression with two arguments: constant values 10 and 20 of type int
```

Static method `Embed` from Code Generator can be used to embed the expression fragment as statement into lexical scope of the multi-line lambda expression:

```csharp
using System;
using static DotNext.Metaprogramming.CodeGenerator;

var greeting = Lambda<Action<string>>(fun =>
{
	Embed<Action<string>>(str => Console.WriteLine("Hello, {0}", str), fun[0]);
});

//the generated code is

new Action<string>(str => Console.WriteLine("Hello, {0}", str)));
```