Direct Access to Memory-Mapped File
====
.NET offers the two representations of the memory-mapped file:
1. [MemoryMappedViewStream](https://docs.microsoft.com/en-us/dotnet/api/system.io.memorymappedfiles.memorymappedviewstream) for sequential access to the memory-mapped file
1. [MemoryMappedViewAccessor](https://docs.microsoft.com/en-us/dotnet/api/system.io.memorymappedfiles.memorymappedviewaccessor) for random access to the memory-mapped file

[MemoryMappedFileExtensions](https://sakno.github.io/dotNext/api/DotNext.IO.MemoryMappedFiles.MemoryMappedFileExtensions.html) class provides unsafe access to the memory-mapped file using the pointer to the virtual memory associated with the file. `CreateDirectAccessor` extension method creates value of [MemoryMappedDirectAccessor](https://sakno.github.io/dotNext/api/DotNext.IO.MemoryMappedFiles.MemoryMappedDirectAccessor.html) value type and gives the following opportunities:
* `Pointer` property provides direct access to the virtual memory
* `Bytes` property represents the content of the memory-mapped file as [Span&lt;byte&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.span-1)

The access is unsafe because it is organized through the pointer and bound checks are omitted. However, this approach allows to achive zero performance overhead in comparison with other views.