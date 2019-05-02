Unstructured Memory Access
====
[UnmanagedMemory](../../api/DotNext.Runtime.InteropServices.UnmanagedMemory.yml) in contrast to [UnmanagedMemory&lt;T&gt;](../../api/DotNext.Runtime.InteropServices.UnmanagedMemory-1.yml) provides unstructured access to the allocated unmanaged memory. Unstructured access means that the allocated block of memory is not represented by some unmanaged type `T`. Instead of that, the memory is represented as a set of bytes. In spite of this, random memory access is protected by boundary checks. Every unmanaged data type in .NEXT Unsafe library is convertible into `UnmanagedMemory` type.

```csharp
using DotNext.Runtime.InteropServices;

var memory = new UnmanagedMemory(16);   //allocates 16 bytes in unmanaged heap
var guid = memory.As<Guid>();   //converts bytes in unmanaged memory into Guid
guid = Guid.NewGuid();
memory.As<Guid>() = guid;   //writes Guid back to the unmanaged memory
memory.Size = 32;   //resize unmanaged memory to 32 bytes. Resizing causes re-allocation.
memory[0] = 42; //change the value of the first byte in the memory 
memory.Dispose();   //releases allocated unmanaged memory
```