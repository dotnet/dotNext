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

# Segmenting Streams
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
using DotNext.IO.Pipelines;
using System.IO.Pipelines;

var pipe = new Pipe();
await pipe.Writer.WriteAsync(Guid.NewGuid());
var result = await pipe.Reader.ReadAsync<Guid>();
```

Starting with version _2.6.0_ there is [BufferWriter](../../api/DotNext.Buffers.BufferWriter.yml) class with extension methods for [IBufferWriter&lt;byte&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.ibufferwriter-1) interface allowing to encode strings and primitives synchronously. Now it's possible to control flushing more granular:
```csharp
using DotNext.IO;
using DotNext.IO.Pipelines;
using System.IO.Pipelines;
using System.Text;

var pipe = new Pipe();
pipe.Writer.Write(Guid.NewGuid());
pipe.Writer.WriteString("Hello, world!".AsSpan(), Encoding.UTF8, StringLengthEncoding.Plain);
await pipe.Writer.FlushAsync();
```

# Decoding Data from ReadOnlySequence
[ReadOnlySequence&lt;byte&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.readonlysequence-1) is a convenient way to read from non-contiguous blocks of memory. However, this API is too low-level and doesn't provide high-level methods for parsing strings and primitives. [SequenceBinaryReader](https://sakno.github.io/dotNext/api/DotNext.IO.SequenceBinaryReader.html) value type is a wrapper for the sequence of memory blocks that provides high-level decoding methods:
```csharp
using DotNext.IO;
using System;
using System.Buffers;
using System.Text;

ReadOnlySequence<byte> sequence = ...;
SequenceBinaryReader reader = IAsyncBinaryReader.Create(sequence);
int i = reader.ReadInt32(BitConverter.IsLittleEndian);
string str = reader.ReadString(StringLengthEncoding.Plain, Encoding.UTF8);
```

# File-Buffering Writer
[FileBufferingWriter](../../api/DotNext.IO.FileBufferingWriter.yml) class can be used as a temporary buffer of bytes when length of the content is not known or dynamic. It's useful in the following situations:
* Synchronous serialization to stream and copying result to another stream asynchronously
* Asynchronous serialization to stream and copying result to another stream synchronously
* Synchronous serialization to stream and copying result to [PipeWriter](https://docs.microsoft.com/en-us/dotnet/api/system.io.pipelines.pipewriter) asynchronously
* Bufferized write to another stream when it's not available immediately
* Bufferize input data and read afterwards
* Dynamic buffer should be represented as [Memory&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.memory-1)

In other words, this class has many similarities with [FileBufferingWriteStream](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.webutilities.filebufferingwritestream). However, `FileBufferingWriter` has a few advantages:
* It depends on .NET Standard rather than ASP.NET Core or .NET Core
* Ability to use custom [MemoryAllocator&lt;T&gt;](../../api/DotNext.Buffers.MemoryAllocator-1.yml) for memory pooling
* Selection between synchronous and asynchronous modes
* Can drain content to [IBufferWriter&lt;byte&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.ibufferwriter-1)
* Ability to read written content as [Stream](https://docs.microsoft.com/en-us/dotnet/api/system.io.stream)
* Ability to represent written content as [Memory&lt;byte&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.memory-1)
* Ability to represent written content as [ReadOnlySequence&lt;byte&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.readonlysequence-1) if it's too large for representation using `Memory<byte>` data type.

The last two features are very useful in situations when the size of memory is not known at the time of the call of write operations. If written content is in memory then returned [Memory&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.memory-1) just references it. Otherwise, `FileBufferingWriter` utilizes memory-mapped file feature and returned [Memory&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.memory-1) represents mapped virtual memory. It's better than using pooled memory because of memory deterministic lifetime and GC pressure.

The following example demonstrates this feature:
```csharp
using DotNext.IO;
using System.Buffers;

using var writer = new FileBufferingWriter();
writer.Write(new byte[] {10, 20, 30});
using (IMemoryOwner<byte> manager = writer.GetWrittenContent())
{
    Memory<byte> memory = manager.Memory;
}
```

If written content is too large to represent it as contiguous block of memory then it can be returned as `ReadOnlySequence<byte>`. Under the hood, this representation uses memory-mapped file as well. However, in constrast to representation of the whole content as `Memory<byte>`, sequence provides access to the linked non-contiguous memory blocks. Each memory block represents only a segment from the whole file. As a result, virtual memory is allocated for mapped segment only. Switching between segments is transparent for the consumer:
```csharp
using DotNext.IO;
using System.Buffers;

using var writer = new FileBufferingWriter();
writer.Write(...);

// The segment size to be mapped to the memory is 1 MB
using (IReadOnlySequenceSource source = writer.GetWrittenContent(1024 * 1024))
{
    ReadOnlySequence<byte> memory = source.Sequence;
}
```

The last option is to use stream-based API to read the written content:
```csharp
using DotNext.IO;
using System.IO;

using var writer = new FileBufferingWriter();
writer.Write(...);

// read data from the stream
using (Stream reader = writer.GetWrittenContentAsStream())
{
}
```

# Encoding/decoding Contiguous Memory
[Span&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.span-1) and [ReadOnlySpan&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.readonlyspan-1) are powerful data types for working with contiguous memory blocks. Random access to memory elements is perfectly supported by their public methods. However, there a lot of cases when sequential access to memory elements required. For instance, the frame of network protocol passed over the wire can be represented as span. Parsing or writing the frame is sequential operation. To cover such use cases, .NEXT exposes simple but powerful types aimed to simplify sequential access to span contents:
* [SpanReader&lt;T&gt;](../../api/DotNext.Buffers.SpanReader-1.yml) provides sequential reading of elements from the memory
* [SpanWriter&lt;T&gt;](../../api/DotNext.Buffers.SpanWriter-1.yml) provides sequential writing of elements to the memory

The following example demonstrates how to encode and decode values to/from the stack-allocated memory:
```csharp
using DotNext.Buffers;
using System;
using static System.Runtime.InteropServices.MemoryMarshal;

Span<byte> memory = stackalloc byte[32];

// encodes int32, int64 and Guid values to stack-allocated memory
var writer = new SpanWriter<byte>(memory);
WriteInt32LittleEndian(writer.Slide(sizeof(int)), 42);
WriteInt64LittleEndian(writer.Slide(sizeof(long)), 42L);
writer.Write<Guid>(Guid.NewGuid());

// decodes int32, int64 and Guid values from stack-allocated memory
var reader = new SpanReader<byte>(memory);
var i32 = ReadInt32LittleEndian(reader.Read(sizeof(int)));
var i64 = ReadInt64LittleEndian(reader.Read(sizeof(long)));
var g = reader.Read<Guid>();
```

# Text Reader for ReadOnlySequence
[ReadOnlySequence&lt;char&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.readonlysequence-1) can be wrapped as an instance of [TextReader](https://docs.microsoft.com/en-us/dotnet/api/system.io.textreader) class to read strings and characters in more convenient way. To do that, you need to instantiate [](../../api/DotNext.IO.TextBufferReader.yml) class and pass the sequence to its constructor.