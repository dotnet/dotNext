Value References
====
Managed pointer (**ref** keyword) and [Span&lt;T&gt;](https://learn.microsoft.com/en-us/dotnet/api/system.span-1) allow to represent access to the referenced value by using interior pointer. However, these data types are restricted by their scope. For instance, if the interior pointer references the field inside the managed object, no need to restrict the scope of that pointer because the object is allocated in the heap.
[ValueReference&lt;T&gt;](xref:DotNext.Runtime.ValueReference`1) and [ReadOnlyValueReference&lt;T&gt;](xref:DotNext.Runtime.ReadOnlyValueReference`1) are alternative representation of the references to the heap-allocated objects without the restrictions described above. You can use these types as a fields of another classes (not limited to **ref struct**).

```csharp
class MyClass
{
    private int field;
    
    public ValueReference<int> GetFieldRef() => new ValueReference<int>(this, ref field);
}
```

`ValueReference<T>` can hold a reference to the following locations:
* Instance field
* Array element
* Static field

If static field is of value type, it must be marked with [FixedAddressValueType](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.fixedaddressvaluetypeattribute) attribute. Otherwise, the behavior is unspecified.
```csharp
class MyClass
{
    [FixedAddressValueType]
    public static int Field;
}

ValueReference<int> reference = new ValueReference<int>(ref MyClass.Field);
reference.Value = 42;
```

# Unmanaged memory reference
`ValueReference<T>` can be used in combination with [Pointer&lt;T&gt;](xref:DotNext.Runtime.InteropServices.Pointer`1) data type to provide a reference to the unmanaged memory:
```csharp
Pointer<long> ptr = NativeMemory.Alloc((uint)sizeof(long));
ValueReference<long> reference = ptr;
reference.Value = 20L;
```

# By-ref parameters in async methods
Async method disallows **ref** parameters because the reference might point the stack-allocated memory of the caller. If async methods needs to suspend, the entire state machine is allocated on the heap, so the stack-allocated pointer becomes invalid. However, some memory allocations are still safe in that case:
* The value allocated in the [unmanaged heap](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.nativememory), memory-mapped file, or anonymous memory page
* A reference to the field which is a part of the object allocated in the managed heap
* A reference to the static field

Async method can use `ValueReference<T>` to simulate by-ref parameter. In combination with [UnmanagedMemory&lt;T&gt;](xref:DotNext.Runtime.InteropServices.UnmanagedMemory`1), async caller doesn't need to allocate the memory for by-ref value in the managed heap, reducing GC pressure:
```csharp
static async Task ByRefMethod(ValueReference<int> reference)
{
    await Task.Yield();
    reference.Value = 42;
}

static async Task AsyncCaller()
{
    using var memory = new UnmanagedMemory<int>();
    await ByRefMethod(memory.Pointer);
    Console.WriteLine(memory.Pointer.Value); // prints 42
}
```