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

var sayHello = Lambda<Action<string>>(fun =>
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

var sayHello = Lambda<Action<string>>(fun =>
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

var sayHello = Lambda<Action<string>>(fun =>
{
	Breakpoint();
	WriteLine(fun[0]);
});
```