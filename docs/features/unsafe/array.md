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
using DotNext.Runtime.InteropServices;

using(var array = new UnmanagedArray<double>(10))
{
    array[0] = 10;
    array[1] = 30;
    foreach(var item in array)
        Console.WriteLine(item);
}
```

The array can be resized on-the-fly. Resizing causes re-allocation of the memory with the copying of the elements from the original location. The new size of the array can be defined through `Length` property.

```csharp
using DotNext.Runtime.InteropServices;

var array = new UnmanagedArray<double>(10); //array.Length == 10L
array[0] = 10;
array[1] = 30;
array.Length = 20;  //causes re-allocation of the array
var i = array[0] + array[1];    //i == 40
array.Dispose();
```

Most of the methods typical to managed arrays such as searching(binary and linear search), filling and sorting are present in an unmanaged array. 
```csharp
using DotNext.Runtime.InteropServices;

var array = new UnmanagedArray<long>(10);
array.Fill(42L);    //set all elements of the array to 42L
array[3] = 19L;
array.IndexOf(19L); // == 3
array.Sort();   //sort array
array.Dispose();
```

# Span and UnmanagedArray
[Span](https://docs.microsoft.com/en-us/dotnet/api/system.span-1) data type from .NET allows to work with managed arrays as well as stack-allocated memory. It is possible to work with unmanaged heap but with some boilerplate code. The following table shows differences between unmanaged array and span:

| Feature | UnmanagedArray | Span |
| ---- | ---- | ---- |
| Managed array as backing store | - | + |
| Random element access | + | + |
| Sort | + | - |
| Search | + | + |
| Copy | + | - |
| Interop with stream | + | - (+ since .NET Standard 2.1) |
| Filling with zeroes | + | + |
| Slicing | + | + |
| Bitwise comparsion | + | ± (only for data types implementing _IEquatable_) |
| Bitwise hash code | + | - |
| Enumeration | + | + |
| 64-bit sized unmanaged array | + | - |
| Can be stored in the field | + | - |
| Volatile operations | + | - |