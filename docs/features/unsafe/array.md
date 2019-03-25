Unmanaged Array
====
[UnmanagedArray](../../api/DotNext.Runtime.InteropServices.UnmanagedArray-1.yml) is a safe way to allocate array in the unmanaged memory. This type provides bound check for each element access as well as typical features of managed array such as sorting and searching.

```csharp
using DotNext.Runtime.InteropServices;

var array = new UnmanagedArray<double>(10); //array of 10 elements

try
{
    //element set
    array[0] = 10;
    array[1] = 30;
    //obtains a pointer to the array element with index 1
    Pointer<double> ptr = array.ElementAt(1);
    ptr.Value = 30;
    //copy to managed heap
    double[] managedArray = array.CopyToManagedHeap();
}
finally
{
    array.Dispose();    //free unmanaged memory
}
```

Unmanaged array supports interoperation with managed arrays and streams.

**foreach** loop is also supported because array implements [IEnumerable](https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.ienumerable-1) interface:
```csharp
using(var array = new UnmanagedArray<double>(10))
{
    array[0] = 10;
    array[1] = 30;
    foreach(var item in array)
        Console.WriteLine(item);
}
```

# Span and UnmanagedArray
[Span](https://docs.microsoft.com/en-us/dotnet/api/system.span-1) data type from .NET allows to work with managed arrays as well as stack-allocated memory. It is possible to work with unmanaged heap but with some boilerplate code. The following table shows differences between unmanaged array and span:

| Feature | UnmanagedArray | Span |
| ---- | ---- | ---- |
| Managed array as backing store | - | + |
| Random element access | + | - |
| Sort | + | - |
| Search | + | - |
| Copy | + | - |
| Interop with stream | + | - (+ since .NET Standard 2.1) |
| Filling with zeroes | + | + |
| Slicing | + | + |
| Bitwise comparsion | + | ± (only for data types implementing _IEquatable_) |
| Bitwise hash code | + | - |
| Enumeration | + | + |
| 64-bit sized unmanaged array | + | - |