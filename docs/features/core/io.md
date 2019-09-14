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
using System.Buffers;
using System.Buffers.Binary;
using System.IO;

//read
using(var fs = new FileStream("content.bin", FileMode.Open, FileAccess.Read, FileShare.Read))
using(var buffer = new ArrayRental<byte>(1024)) //rent the buffer
{
    var lengthInBytes = BinaryPrimitives.ReadInt64LittleEndian(fs.ReadBytes(sizeof(long)));
    var str = await fs.ReadStringAsync(lengthInBytes, Encoding.UTF8);
}
```

# Segmenting streams
