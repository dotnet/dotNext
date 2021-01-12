Loops
====
Metaprogramming library provides construction of **for**, **foreach** and **while** loops. The lexical scope constructor may have optional parameter of type [LoopContext](../../api/DotNext.Metaprogramming.LoopContext.yml) that can be used to leave outer loop from inner loop. There is no equivalent instruction in C# except unconditional control transfer using **goto**.

`Continue()` or `Break()` methods from [CodeGenerator](../../api/DotNext.Metaprogramming.CodeGenerator.yml) are used to pass the control to the next iteration or out of scope respectively.

# foreach Loop
**foreach** statement may accept any expression of type implementing [IEnumerable](https://docs.microsoft.com/en-us/dotnet/api/system.collections.ienumerable) or [IEnumerable&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.ienumerable-1) interface, or having public instance parameterless method `GetEnumerator()` which return type has the public instance property `Current` and public instance parameterless method `MoveNext()`.

```csharp
using System;
using static DotNext.Metaprogramming.CodeGenerator;

Lambda<Action<string>>(fun => 
{
    ForEach(fun[0], ch =>
    {
        WriteLine(ch);
    });
});

//generated code is

new Action<string>(str => 
{
    foreach (char ch in str)
        Console.WriteLine(ch);
});
```

`ch` variable provides access to current element in the collection.

# await foreach Loop
**await foreach** statement allows to enumerate over elements in asynchronous streams implementing [IAsyncEnumerable&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.iasyncenumerable-1) interface. 

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static DotNext.Metaprogramming.CodeGenerator;

AsyncLambda<Func<IAsyncEnumerable<char>, Task>>(fun => 
{
    AwaitForEach(fun[0], ch =>
    {
        WriteLine(ch);
    });
});

//generated code is

new Func<IAsyncEnumerable<char>, Task>(async str => 
{
    await foreach (char ch in str)
        Console.WriteLine(ch);
});
```

This type of statement is allowed within async lambda expression only.

# while Loop
**while** loop statement is supported in two forms: _while-do_ or _do-while_. Both forms are representing regular **while** loop existing in most popular languages such as C#, C++ and C. 

```csharp
using System;
using System.Linq.Expressions;
using static DotNext.Metaprogramming.CodeGenerator;
using static DotNext.Linq.Expressions.ExpressionBuilder;

Lambda<Fun<long, long>>((fun, result) => 
{
    var arg = fun[0];
    Assign(result, 1L);  //result = 1L;
    While((Expression)(arg.AsDynamic() > 1L), () => 
	{
        Assign(result, arg.AsDynamic()-- * fun.Result);  //result *= arg--;
    });
});

//generated code is

new Func<long, long>(arg => 
{
    var result = 1L;
    while (arg > 1L)
        result *= arg--;
    return result;
});
```

**do-while** loop can be constructed using `DoWhile` method.

# for Loop
**for** loop implementation in Metaprogramming library has many similarities with implementation in other C-like languages with one exception: _increment_ (or _post iteration_) statement is optional, has access to the local variables declared inside of the loop and can be a compount statement. This statement is separated from main loop body with a special method call `StartIteratorBlock`.

```csharp
using System;
using static DotNext.Metaprogramming.CodeGenerator;
using static DotNext.Linq.Expressions.ExpressionBuilder;

Lambda<Fun<long, long>>((fun, result) => 
{
    Assign(result, 1L);  //result = 1L;
    For(fun[0], i => i.AsDynamic() > 1L, i => PostDecrementAssign(i), i => 
    {
        Assign(result, i.AsDynamic() * result);    //result *= i;
    });
});

//generated code is

new Func<long, long>(arg => 
{
    var result = 1L;
    for (var i = arg; i > 1L; i--)
        result *= i;
    return result;
});
```

# Plain Loop
Plain loop is similar to `while(true)` loop and doesn't have built-in loop control tools. Developer need to control loop execution by calling `Continue()` and `Break` manually.

```csharp
using System;
using static DotNext.Metaprogramming.CodeGenerator;
using static DotNext.Linq.Expressions.ExpressionBuilder;

Lambda<Fun<long, long>>((fun, result) => 
{
    var arg = fun[0];
    Assign(result, 1L);  //result = 1L;
    Loop(() => 
    {
        If((Expression)(arg.AsDynamic() > 1L))
            .Then(() => Assign(result, arg.AsDynamic()-- * result))
            .Else(Break)  //break;
        .End();
    });
});

//generated code is
new Func<long, long>(arg => 
{
    var result = 1L;
    while (true)
        if(arg > 1L)
            result *= arg--;
        else
            break;
    return true;
});
```