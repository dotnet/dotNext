I/O Enhancements
====
[BinaryReader](https://docs.microsoft.com/en-us/dotnet/api/system.io.binaryreader) and [BinaryWriter](https://docs.microsoft.com/en-us/dotnet/api/system.io.binarywriter) are aimed to high-level parsing or writing of stream content. These classes have several drawbacks:
* They don't provide asynchronous alternatives, especially for string
* They should be allocated on the heap
* They use internal buffer that is not accessible publicly

The situation is worse when you need to have parse and write into the stream in the same time. In this case you need both `BinaryReader` and `BinaryWriter` with their own allocated buffers. So you have fourfold price: instance of writer and its internal buffer in the form of the array and instance of reader with its internal buffer. It is more complicated if your stream contains strings of different encodings.

[StreamExtension](https://sakno.github.io/dotNext/api/DotNext.IO.StreamExtensions.html) class contains extension methods for high-level parsing of stream content as well as typed writers with the following benefits:
* You can share the same buffer between reader and writer methods
* You can manage how the buffer should be allocated: from [array pool](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1) or using **new** keyword
* You can use different encodings to encode or decode strings in the same stream
* Endianness of value types under your control

The following example demonstrates how to use these methods:
```csharp
using DotNext.Buffers;
using DotNext.IO;
using System.Buffers.Binary;
using System.IO;

//read
using var fs = new FileStream("content.bin", FileMode.Open, FileAccess.Read, FileShare.Read);
using var buffer = new ArrayRental<byte>(1024); //rent the buffer
var str = await fs.ReadStringAsync(StringLengthEncoding.Plain, Encoding.UTF8);
```

String encoding and decoding methods support various length encoding styles using [StringLengthEncoding](../../api/DotNext.IO.StringLengthEncoding.yml) enum type. As a result, you can prefix string with its length automatically.

# Segmenting streams
In some cases you may need to hide the entire stream from the callee for the reading operation. This can be necessary to protect underlying stream from accidental seeking. [StreamSegment](https://sakno.github.io/dotNext/api/DotNext.IO.StreamSegment.html) do the same for streams as [ArraySegment](https://docs.microsoft.com/en-us/dotnet/api/system.arraysegment-1) for arrays.

> [!NOTE]
> Stream segment is read-only stream that cannot be used for writes

The segment can be reused for multiple consumers because its position and length can be adjusted for the same instance.

```csharp
using DotNext.IO;
using System.IO;

var fs = new FileStream("content.bin", FileMode.Open, FileAccess.Read, FileShare.Read);
using var segment = new StreamSegment(fs);
foreach(Action<Stream> consumer in consumers)
{
    segment.Adjust(10L, 1024L); //fs is limited to the segment limited by the offset of 10 from the beginning of the stream and length of 1024 bytes
    consumer(segment);
}
```

# Pipelines
[System.IO.Pipelines](https://docs.microsoft.com/en-us/dotnet/api/system.io.pipelines) knowns as high-performance alternative to .NET streams. However, it doesn't have built-in helpers for encoding and decoding strongly-typed data such as blittable value types and strings that are provided by [BinaryReader](https://docs.microsoft.com/en-us/dotnet/api/system.io.binaryreader) and [BinaryWriter](https://docs.microsoft.com/en-us/dotnet/api/system.io.binarywriter) classes. .NEXT I/O library provides API surface in the form of extensions methods that cover all these needs and turns I/O pipelines into first-class citizen in the world of high-level I/O operations. With these methods you can easily swith from streams to pipes without increasing complexity of code.

The following example demonstrates string encoding and decoding using I/O pipelines:
```csharp
using DotNext.IO;
using DotNext.IO.Pipelines;
using System.IO.Pipelines;

const string value = "Hello, world!";
var pipe = new Pipe();
await pipe.Writer.WriteStringAsync(value.AsMemory(), Encoding.UTF8, 0, StringLengthEncoding.Plain);
var result = await pipe.Reader.ReadStringAsync(StringLengthEncoding.Plain, Encoding.UTF8);
```

In advance to strings, the library supports decoding and encoding values of arbitrary blittable value types.
```csharp
using DotNext.IO;
using DotNext.IO.Pipelines;
using System.IO.Pipelines;

const string value = "Hello, world!";
var pipe = new Pipe();
await pipe.Writer.WriteAsync(Guid.NewGuid());
var result = await pipe.Reader.ReadAsync<Guid>();
```

# File-Buffering Writer
[FileBufferingWriter](../../api/DotNext.IO.FileBufferingWriter.yml) class can be used as a temporary buffer of bytes when length of the content is not known or dynamic. It's useful in the following situations:
* Synchronous serialization to stream and copying result to another stream asynchronously
* Asynchronous serialization to stream and copying result to another stream synchronously
* Synchronous serialization to stream and copying result to [PipeWriter](https://docs.microsoft.com/en-us/dotnet/api/system.io.pipelines.pipewriter) asynchronously
* Bufferized write to another stream when it's not available immediately
* Dynamic buffer should be represented as [Memory&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.memory-1)

In other words, this class has many similarities with [FileBufferingWriteStream](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.webutilities.filebufferingwritestream). However, `FileBufferingWriter` has a few advantages:
* It depends on .NET Standard rather than ASP.NET Core or .NET Core
* Ability to use custom [MemoryAllocator&lt;T&gt;](../../api/DotNext.Buffers.MemoryAllocator-1.yml) for memory pooling
* Selection between synchronous and asynchronous modes
* Can drain content to [IBufferWriter&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.ibufferwriter-1)
* Ability to represent written content as [Memory&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.memory-1)

The last feature is very useful in situations when the size of memory is not known at the time of the call of write operations. If written content is in memory then returned [Memory&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.memory-1) just references it. Otherwise, `FileBufferingWriter` utilizes memory-mapped file feature and returned [Memory&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.memory-1) represents mapped virtual memory. It's better than using pooled memory because of memory deterministic lifetime and GC pressure.

The following example demonstrates this feature:
```csharp
using DotNext.IO;
using System.Buffers;

using var writer = new FileBufferingWriter();
writer.Write(new byte[] {10, 20, 30});
using (MemoryManager<byte> manager = writer.GetWrittenContent())
{
    Memory<byte> memory = manager.Memory;
}
```