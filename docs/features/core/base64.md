Base64 Decoding
====
Converting base64-encoded binary content in streaming scenarios may be difficult because .NET standard library provides only [low-level operations](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.text.base64) for decoding. When the input stream has unpredictable size it's very hard to use such API and reduce memory allocations during decoding in the same time. Moreover, base64-encoded binary content can be represented in two forms:
* As UTF-8 encoded characters in the form of the sequence of bytes
* As Unicode encoded characters in the form of the sequence of chars

[Base64Decoder](xref:DotNext.Text.Base64Decoder) type is specially designed for decoding base64-encoded content in streaming scenarios because it maintains the state in the form of the buffer when input fragment cannot be fully decoded.

The following example demonstrates how to decode base64-encoded content represented by UTF-8 encoded characters from the stream:
```csharp
using DotNext;
using DotNext.Text;
using System;
using System.IO;

static void WriteToStream(ReadOnlySpan<byte> decodedBytes, MemoryStream output)
    => output.Write(decodedBytes);

using Stream input = ...; // stream containing UTF-8 characters of base-64 encoded content
using var output = new MemoryStream();
var decoder = new Base64Decoder();
Span<byte> buffer = stackalloc byte[128];
for (int count; (count = input.Read(buffer)) > 0; )
{
    decoder.Decode(buffer.Slice(0, count), &WriteToStream, output);
}
```