Loops
====
Metaprogramming library provides construction of `for`, `foreach` and `while` loops. The lexical scope constructor may have optional parameter of type [LoopContext](../../api/DotNext.Metaprogramming.LoopContext.yml) that can be used to leave outer loop from inner loop. There is no equivalent instruction in C# except unconditional control transfer using `goto`.

`Continue()` or `Break()` methods from [CodeGenerator](../../api/DotNext.Metaprogramming.CodeGenerator.yml) are used to pass the control to the next iteration or out of scope respectively.

# foreach Loop
`foreach` statement may accept any expression of type implementing `System.Collections.IEnumerable` or `System.Collections.Generic.IEnumerable<T>` interface, or having public instance parameterless method `GetEnumerator()` which return type has the public instance property `Current` and public instance parameterless method `MoveNext()`.

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
    foreach(char ch in str)
        Console.WriteLine(ch);
});
```

`ch` variable provides access to current element in the collection.

# while Loop
`while` loop statement is supported in two forms: `while-do` or `do-while`. Both forms are representing regular `while` loop existing in most popular languages such as C#, C++ and C. 

```csharp
using System;
using static DotNext.Metaprogramming.CodeGenerator;
using U = DotNext.Linq.Expressions.UniversalExpression;

Lambda<Fun<long, long>>((fun, result) => 
{
    var arg = (U)fun[0];
    Assign(result, 1L);  //result = 1L;
    While(arg > 1L, () => 
	{
        Assign(result, arg-- * fun.Result);  //result *= arg--;
    });
});

//generated code is

new Func<long, long>(arg => 
{
    var result = 1L;
    while(arg > 1L)
        result *= arg--;
    return result;
});
```

`do-while` loop can be constructed using `DoWhile` method.

# for Loop
`for` loop implementation in Metaprogramming library has many similarities with implementation in other C-like languages with one exception: _increment_ (or _post iteration_) statement is optional, has access to the local variables declared inside of the loop and can be a compount statement. This statement is separated from main loop body with a special method call `StartIteratorBlock`.

```csharp
using System;
using static DotNext.Metaprogramming.CodeGenerator;
using U = DotNext.Linq.Expressions.UniversalExpression;

Lambda<Fun<long, long>>((fun, result) => 
{
    Assign(result, 1L);  //result = 1L;
    For(fun[0], i => (U)i > 1L, i => PostDecrementAssign(i), i => 
    {
        Assign(result, (U)i * result);    //result *= i;
    });
});

//generated code is

new Func<long, long>(arg => 
{
    var result = 1L;
    for(var i = arg; i > 1L; i--)
        result *= i;
    return result;
});
```

# Plain Loop
Plain loop is similar to `while(true)` loop and doesn't have built-in loop control tools. Developer need to control loop execution by calling `Continue()` and `Break` manually.

```csharp
using System;
using static DotNext.Metaprogramming.CodeGenerator;
using U = DotNext.Linq.Expressions.UniversalExpression;

Lambda<Fun<long, long>>((fun, result) => 
{
    var arg = (U)fun[0];
    Assign(result, 1L);  //result = 1L;
    Loop(() => 
    {
        If(arg > 1L)
            .Then(() => Assign(result, arg-- * result))
            .Else(Break)  //break;
        .End();
    });
});

//generated code is
new Func<long, long>(arg => 
{
    var result = 1L;
    while(true)
        if(arg > 1L)
            result *= arg--;
        else
            break;
    return true;
});
```