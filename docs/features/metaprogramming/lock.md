lock Statement
====
The **lock** statement acquires the mutual-exclusion lock for a given object, executes a statement block, and then releases the lock. While a lock is held, the thread that holds the lock can again acquire and release the lock. Any other thread is blocked from acquiring the lock and waits until the lock is released.

> [!NOTE]
> This statement is supported since version 0.2 of Metaprogramming library

The statement can be constructed using _Lock_ method from any scope control object:

```csharp
using System.Text;
using DotNext.Metaprogramming;

LambdaBuilder<Action<StringBuilder>>.Build(fun =>
{
    fun.Lock(fun.Parameters[0], @lock => 
    {
        @lock.Call(fun.Parameters[0], nameof(StringBuilder.Append), 'a');
    });
})

//the generated code is

new Action<StringBuilder>(arg => 
{
    lock(arg)
    {
        arg.Append('a');
    }
});
```