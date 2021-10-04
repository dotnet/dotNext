Efficient String Building
====
Dynamic string building with zero allocation overhead is provided on top of [BufferWriterSlim&lt;char&gt;](xref:DotNext.Buffers.BufferWriterSlim`1). The type originally developed for working with dynamic buffers. When instantiated as a buffer of characters, it can be naturally used as a string builder. The main difference with [StringBuilder](https://docs.microsoft.com/en-us/dotnet/api/system.text.stringbuilder) is that the `BufferWriterSlim<char>` allows to use stack-allocated and pooled memory. As a result, it doesn't require reallocations of the internal buffer on the heap. A new [string interpolation](https://devblogs.microsoft.com/dotnet/string-interpolation-in-c-10-and-net-6/) mechanism introduced in C# 10 is fully supported by `BufferWriterSlim<char>` data type:
```csharp
using DotNext.Buffers;

using var writer = new BufferWriterSlim<char>(stackalloc char[128]); // preallocate initial buffer on the stack

int x = 10, y = 20;
writer.WriteString($"{x} + {y} = {x + y}");
writer.WriteLine();
writer.Write("Hello, world!");
writer.WriteFormattable(42, "X");

Span<char> writtenSpan = writer.WrittenSpan;
string result = writer.ToString();
```

`BufferWriterSlim<char>` is ref-like **struct** so it's not suitable for async scenarios. However, it's possible to use [IBufferWriter&lt;char&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.ibufferwriter-1) as a dynamic buffer of characters for building strings. Read [this](../io/buffers.md) article to find a workaround.