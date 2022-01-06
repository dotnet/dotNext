Soft Reference
====
[SoftReference](https://docs.oracle.com/javase/10/docs/api/java/lang/ref/SoftReference.html) is a common concept in Java to organize memory-sensitive caches. Typically, GC in Java collects softly referenced objects when its algorithm decides that the memory is low enough. Common Language Runtime offers weak references only in the form of [WeakReference](https://docs.microsoft.com/en-us/dotnet/api/system.weakreference) class or [GC handles](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.gchandle).

.NEXT introduces [SoftReference&lt;T&gt;](xref:DotNext.Runtime.SoftReference`1) data type to fill this gap. However, its behavior has some differences in contrast to implementation in Java:
* If referenced object is of Generation 0 or 1 from GC perspective, then soft reference behaves like a strong reference to the object. It means that young object referenced by soft reference cannot be reclaimed by GC.
* If referenced object is of Generation 2, soft reference switches to weak reference mode so the object can be reclaimed by GC

As a result, softly referenced object can be reclaimed during Background or Full-Blocking GC to collect objects in Generation 2.

The actual lifetime of the soft reference can be tuned in more advanced way using [SoftReferenceOptions](xref:DotNext.Runtime.SoftReferenceOptions) class:
* It is possible how many collections of objects in Gen2 can survive sofly referenced object
* Explicitly define memory pressure: if allocated memory in Gen2 exceedes the limit then the object will be available for GC

The following example demonstrates how to create a soft reference to the object:
```csharp
using System.Text;
using DotNext.Runtime;

SoftReference<StringBuilder> reference = new(new StringBuilder(), SoftReferenceOptions.Default);
```