Direct Access to Memory-Mapped File
====
.NET offers the two representations of the memory-mapped file:
1. [MemoryMappedViewStream](https://docs.microsoft.com/en-us/dotnet/api/system.io.memorymappedfiles.memorymappedviewstream) for sequential access to the memory-mapped file
1. [MemoryMappedViewAccessor](https://docs.microsoft.com/en-us/dotnet/api/system.io.memorymappedfiles.memorymappedviewaccessor) for random access to the memory-mapped file

[MemoryMappedFileExtensions](../../api/DotNext.IO.MemoryMappedFiles.MemoryMappedFileExtensions.yml) class provides additionals views of memory-mapped files. 

# Direct Access
`CreateDirectAccessor` extension method creates value of [MemoryMappedDirectAccessor](../../api/DotNext.IO.MemoryMappedFiles.MemoryMappedDirectAccessor.yml) value type and gives the following benefits:
* `Pointer` property provides direct access to the virtual memory
* `Bytes` property represents the content of the memory-mapped file as [Span&lt;byte&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.span-1)

The access is unsafe because it is organized through the pointer and bound checks are omitted. However, this approach allows to achive zero performance overhead in comparison with other views.

# Compatibility with Memory&lt;byte&gt;
`CreateMemoryAccessor` extension method creates object of [IMappedMemoryOwner](../../api/DotNext.IO.MemoryMappedFiles.IMappedMemoryOwner.yml) interface that provides access to mapped file memory via [Memory&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.memory-1) data type. This is the main reason to use such representation of memory-mapped file. This approach has the main drawback: OS allocates pages of virtual memory for the specified segment of memory-mapped file so `Memory<byte>` can represent contiguous block of memory. If segment is too large then you can get [OutOfMemoryException](https://docs.microsoft.com/en-us/dotnet/api/system.outofmemoryexception) exception. This problem can be avoided as described below.

# Compatibility with ReadOnlySequence&lt;byte&gt;
If file too large then attempt to map the whole file to memory is not possible. As a result, you cannot work with large files via `Memory<byte>` or `Span<byte>` data type. The only way is to represent memory-mapped file as non-contiguous segments of memory. .NET standard library offers [ReadOnlySequence&lt;byte&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.readonlysequence-1) data type to work with such structures. [ReadOnlySequenceAccessor](../../api/DotNext.IO.MemoryMappedFiles.ReadOnlySequenceAccessor.yml) class can be used to obtain `ReadOnlySequence<byte>` over segments of memory-mapped file. The implementation provides the following features:
* Virtual memory is allocated only for the segment of file. The size of the segment can be defined as an argument of `ReadOnlySequenceAccessor` constructor.
* Lazy switching between segments
* Switching between segments is automatic and doesn't require involvement from API consumer

Limitations are also presented:
* Instance of [ReadOnlySequenceAccessor](../../api/DotNext.IO.MemoryMappedFiles.ReadOnlySequenceAccessor.yml) class is not thread-safe and must not be shared between threads without synchronization. Alternatively, you can obtain the source for each thread and use them separately. However, this approach will cause allocation of virtual memory for the segment in each thread.
* Multiple instances of `ReadOnlySequence<byte>` obtained from single source share the same state and must not be shared across multiple threads. Moreover, usage of different sequences obtained from the same source can cause unwanted switches between segments of memory-mapped files.
* The memory represents by the sequence is read-only. So you cannot use this accessor to modify contents of the file.