lock Statement
====
The **lock** statement acquires the mutual-exclusion lock for a given object, executes a statement block, and then releases the lock. While a lock is held, the thread that holds the lock can again acquire and release the lock. Any other thread is blocked from acquiring the lock and waits until the lock is released.

The statement can be constructed using _Lock_ method from any scope control object:

```csharp
using System.Text;
using static DotNext.Metaprogramming.CodeGenerator;

Lambda<Action<StringBuilder>>(fun =>
{
    Lock(fun[0], () => 
    {
        Call(fun[0], nameof(StringBuilder.Append), 'a');
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