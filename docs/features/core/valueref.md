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