---
name: dotnext-memory
description: Allocation-free memory routines from the DotNext and DotNext.Unsafe NuGet packages -
  SpanOwner (safe stackalloc with pooled fallback), BufferWriterSlim (stack-allocated
  StringBuilder/buffer replacement with string interpolation), PoolingBufferWriter,
  PoolingArrayBufferWriter, SparseBufferWriter, and the MemoryAllocator abstraction over ArrayPool,
  plain arrays and unmanaged memory. Use when reducing allocations in .NET hot paths; when
  stackalloc length comes from input; when replacing StringBuilder, MemoryStream,
  RecyclableMemoryStream, ArrayBufferWriter or manual ArrayPool rent/return code; or when making
  the memory allocation strategy configurable.
license: MIT
metadata:
  dotnext-version: "6.x"
---

# Memory Routines

## Before you start

1. Check which version of `DotNext` and `DotNext.Unsafe` the project references (`.csproj` or
   `Directory.Packages.props`). This skill describes 6.x. For older versions, verify each
   member you use against the XML docs/IntelliSense of the referenced version.
2. The types below are in several namespaces. Add the `using` shown next to each type.

Most of the types are in `DotNext` package. In case of unmanaged memory allocations, add `DotNext.Unsafe` dependency as well.

## Use Cases
- Avoid **stackalloc** which length is an input from the untrusted source: user input, request parameter, etc. If the length is expected to be small, but you need safe fallback, use `SpanOwner<T>` (`DotNext.Buffers`) data type.
  - Always declare it with `using`. When the length exceeds the threshold, `SpanOwner<T>` rents the memory from the pool, and `Dispose` returns it. For a stack-allocated span, `Dispose` does nothing.
  - Choose the threshold in bytes, not elements: divide by `Unsafe.SizeOf<T>()` for other element types.
  - Canonical example:
```cs
using DotNext.Buffers;

public static void Encode(ReadOnlySpan<char> input, Stream output, Encoding encoding)
{
    const int stackallocThreshold = 512; // in bytes; keep it small, the stack is limited
    int maxByteCount = encoding.GetMaxByteCount(input.Length);
    using SpanOwner<byte> buffer = (uint)maxByteCount <= (uint)stackallocThreshold
        ? stackalloc byte[maxByteCount]
        : new SpanOwner<byte>(maxByteCount);

    var bytesWritten = encoding.GetBytes(input, buffer.Span);
    output.Write(buffer.Span.Slice(0, bytesWritten));
}
```

- Consider `DotNext.Buffers.BufferWriterSlim<T>` type in combination with extension methods from `DotNext.Buffers.Text.CharBuffer` class instead of `System.Text.StringBuilder` in synchronous code because its initial buffer can be allocated on the stack. If the buffer overflows, the writer uses memory allocator to rent the memory (by default it uses `ArrayPool<T>.Shared`). String interpolation for this type is exposed by the `Interpolate` extension method from `DotNext.Text.StringInterpolation` class.
  - The initial buffer must have the same element type as the writer: `stackalloc char[...]` for `BufferWriterSlim<char>`.
  - Keep `Interpolate` outside of the `try` block: calling it on a `BufferWriterSlim<char>` local inside `try`/`finally` fails to compile (CS8347/CS8350), and `using var writer` is not allowed either (CS1657, a `using` variable can't be passed by `ref`). If `Interpolate` throws, the rented buffer is not returned to the pool, which is harmless.
  - `WrittenSpan` becomes invalid after `Dispose()`. Consume or copy it before.
  - Canonical example:
```cs
using DotNext.Buffers;
using DotNext.Text;

var writer = new BufferWriterSlim<char>(stackalloc char[256]);
writer.Interpolate($"Hello, {name}!");
try
{
    ConsumeWrittenChars(writer.WrittenSpan);
}
finally
{
    writer.Dispose();
}
```

- Consider `PoolingBufferWriter<T>` (`DotNext.Buffers`) for async scenarios, where `BufferWriterSlim<T>` cannot be applied because it's **ref struct**. Set the expected upper bound of the written data via the `Capacity` init-only property, because the constructor only accepts the memory allocator: `new PoolingBufferWriter<byte>(allocator) { Capacity = 4096 }`. The buffer grows automatically in case of overflow, but each growth rents a larger buffer, copies the written data and returns the smaller buffer to the pool, where it remains retained. Frequent growth therefore costs copying and inflates the pool, so a good initial capacity matters.
  - Use `PoolingArrayBufferWriter<T>` (`DotNext.Buffers`) instead when the consumer needs the written data as an array: its `WrittenArray` property returns `ArraySegment<T>`, for example to pass to an API that accepts `byte[]` with offset and count. It rents arrays from `ArrayPool<T>`.
- Consider `SparseBufferWriter<T>` (`DotNext.Buffers`) if there is no way to guess the initial capacity, and the contiguous buffer is not a requirement. It chains chunks instead of growing a single buffer, so it never copies the written data on growth. The written memory can be enumerated chunk by chunk (`foreach (ReadOnlyMemory<T> chunk in writer)`), copied with `CopyTo`, or obtained as `System.Buffers.ReadOnlySequence<T>` through its `ISupplier<ReadOnlySequence<T>>` implementation. This type is not limited to bytes in contrast to `RecyclableMemoryStream` from `Microsoft.IO.RecyclableMemoryStream` package.
  - Write through its public `Write`/`Add` methods only. Its `IBufferWriter<T>` members are implemented explicitly because using them can leave holes in the sparse buffer.

- Abstract away memory rental and allocation by using `MemoryAllocator<T>` delegate (`DotNext.Buffers`). It can be passed to the types mentioned above, so your application can be configured to use one of the predefined allocation strategies:
  - `System.Buffers.ArrayPool<T>.Shared` which is exposed by `MemoryAllocator<T>.Default` static property
  - Plain array allocation without pooling, which is exposed by `MemoryAllocator<T>.Array` static property
  - Unmanaged heap allocation, which is exposed by `MemoryAllocator<T>.Unmanaged` static extension property, which requires a reference to `DotNext.Unsafe` package
  - A custom `ArrayPool<T>` or `MemoryPool<T>` can be converted with the `ToAllocator()` extension method