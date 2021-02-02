Exchanger
====
[AsyncExchanger](xref:DotNext.Threading.AsyncExchanger`1) is a synchronization point at which two asynchronous flows can cooperate through swapping elements within pairs. Each flow provides some object when entering exchange, matches with a partner flow, and receives its partner's object on return. Exchanges may be applicable for building genetic algorithms and pipeline designs, where first flow acts as a producer and the second as a consumer. In contrast to [Pipe](https://docs.microsoft.com/en-us/dotnet/api/system.io.pipelines.pipe) from I/O Pipelines library, Exchanger supports arbitrary data type because it's a generic class.

The following example demonstrates how to use Exchanger to swap buffers between two long-running async tasks.
```csharp
using DotNext.Threading;
using System.Threading;
using System.Threading.Tasks;

static async Task ProduceAsync(AsyncExchanger<Memory<char>> exchanger, CancellationToken token)
{
    Memory<char> buffer = new byte[1024];
    while (!token.IsCancellationRequested)
    {
        string str = Console.ReadLine();
        str.AsSpan().CopyTo(buffer.Span);
        buffer = await exchanger.ExchangeAsync(buffer, token);
    }
}

static async Task ConsumeAsync(AsyncExchanger<Memory<char>> exchanger, CancellationToken token)
{
    Memory<char> buffer = new byte[1024];
    while (!token.IsCancellationRequested)
    {
        buffer = await exchanger.ExchangeAsync(buffer, token);
        string str = new string(buffer.Span);
        Console.WriteLine(str);
    }
}
```
