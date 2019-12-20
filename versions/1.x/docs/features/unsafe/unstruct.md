Unstructured Memory Access
====
[UnmanagedMemory](../../api/DotNext.Runtime.InteropServices.UnmanagedMemory.yml) in contrast to [UnmanagedMemory&lt;T&gt;](../../api/DotNext.Runtime.InteropServices.UnmanagedMemory-1.yml) provides unstructured access to the allocated unmanaged memory. Unstructured access means that the allocated block of memory is not represented by some blittable value type. Instead of that, the memory is represented as a set of bytes.

```csharp
using DotNext.Runtime.InteropServices;

using(var memory = new UnmanagedMemory(16))   //allocates 16 bytes in unmanaged heap
{
    Guid guid = memory.Pointer.As<Guid>().Value;   //converts bytes in unmanaged memory into Guid
    guid = Guid.NewGuid();
    memory.Pointer.As<Guid>().Value = guid;   //writes Guid back to the unmanaged memory
    memory.Reallocate(32);  //resize unmanaged memory to 32 bytes. Resizing causes re-allocation.
    memory.Bytes[0] = 42; //change the value of the first byte in the memory 
}
```