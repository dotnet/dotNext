using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Runtime;

/// <summary>
/// Provides a reference to the memory location.
/// </summary>
/// <remarks>
/// This type encapsulates the reference to the memory location where the value is stored.
/// The reference can be used in async context and stored in a field or a regular class in
/// contrast to <c>ref</c>-structs.
/// </remarks>
/// <typeparam name="TValue">The type of the value stored at a memory location.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly unsafe struct Reference<TValue>
{
    /*
     * if owner is null then accessor is of type delegate*<ref TValue>,
     * if owner is TValue[] then accessor is of type nint;
     * if owner is the same as Sentinel.Instance then accessor is of type TValue*;
     * otherwise, access is of type delegate*<object, ref TValue>.
     */
    private readonly void* accessor;
    private readonly object? owner;

    private Reference(object owner, delegate*<object, ref TValue> accessor)
    {
        Debug.Assert(owner is not null);
        Debug.Assert(accessor != null);

        this.owner = owner;
        this.accessor = accessor;
    }

    internal Reference(delegate*<ref TValue> accessor)
    {
        Debug.Assert(accessor != null);

        owner = null;
        this.accessor = accessor;
    }

    internal Reference(TValue[] array, nint index)
    {
        Debug.Assert(array is not null);

        owner = array;
        accessor = (void*)index;
    }

    internal Reference(void* pointer)
    {
        Debug.Assert(pointer != null);

        owner = Sentinel.Instance;
        accessor = pointer;
    }

    internal static Reference<TValue> Create<TOwner>(TOwner owner, delegate*<TOwner, ref TValue> accessor)
        where TOwner : class
        => new(owner, (delegate*<object, ref TValue>)accessor);

    /// <summary>
    /// Gets a value indicating that this reference is valid.
    /// </summary>
    public bool IsValid => accessor != null || owner is TValue[];

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private ref TValue RawValue
    {
        get
        {
            ref TValue result = ref Unsafe.NullRef<TValue>();

            // array index may be zero so check on array type first
            if (owner is TValue[] array)
            {
                result = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), (nint)accessor);
            }
            else if (accessor is null)
            {
                // leave getter
            }
            else if (owner is null)
            {
                result = ref ((delegate*<ref TValue>)accessor)();
            }
            else if (ReferenceEquals(owner, Sentinel.Instance))
            {
                result = ref Unsafe.AsRef<TValue>(accessor);
            }
            else
            {
                result = ref ((delegate*<object, ref TValue>)accessor)(owner);
            }

            return ref result;
        }
    }

    /// <summary>
    /// Gets a reference to the memory location where the value is stored.
    /// </summary>
    /// <exception cref="NullReferenceException">This reference is not valid.</exception>
    public ref TValue Target
    {
        get
        {
            ref TValue result = ref RawValue;
            Intrinsics.ThrowIfNull(in result);
            return ref result;
        }
    }

    /// <summary>
    /// Gets a span with the single element representing the underlying value.
    /// </summary>
    /// <remarks>
    /// The returned span is always of size 1 for the valid reference. If this reference is invalid
    /// then returned span is empty.
    /// </remarks>
    public Span<TValue> Span
    {
        get
        {
            ref TValue result = ref RawValue;
            return Unsafe.IsNullRef(ref result) ? Span<TValue>.Empty : MemoryMarshal.CreateSpan(ref result, 1);
        }
    }

    /// <summary>
    /// Converts the underlying value to a string.
    /// </summary>
    /// <returns>The string representing underlying value.</returns>
    public override string? ToString()
    {
        ref var value = ref RawValue;
        return Unsafe.IsNullRef(ref value) ? null : value?.ToString();
    }
}

/// <summary>
/// Provides factory methods for creating references.
/// </summary>
public static class Reference
{
    /// <summary>
    /// Creates a reference to the value stored in a static field.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="getter">The function providing the location of the value in the memory.</param>
    /// <returns>The reference to the memory location where the value is stored.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="getter"/> is <see langword="null"/>.</exception>
    [CLSCompliant(false)]
    public static unsafe Reference<TValue> Create<TValue>(delegate*<ref TValue> getter)
    {
        if (getter is null)
            throw new ArgumentNullException(nameof(getter));

        return new(getter);
    }

    /// <summary>
    /// Creates a reference to the value stored in the object.
    /// </summary>
    /// <typeparam name="TOwner">The type of the object that stores the value.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="owner">The object that stores the value.</param>
    /// <param name="getter">The function providing the location of the value in the memory.</param>
    /// <returns>The reference to the memory location where the value is stored.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="owner"/> is <see langword="null"/>;
    /// or <paramref name="getter"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="owner"/> is a single-dimensional array.
    /// </exception>
    [CLSCompliant(false)]
    public static unsafe Reference<TValue> Create<TOwner, TValue>(TOwner owner, delegate*<TOwner, ref TValue> getter)
        where TOwner : class
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (getter is null)
            throw new ArgumentNullException(nameof(getter));

        if (owner is Array and { Rank: 1 })
            throw new ArgumentException(ExceptionMessages.ObjectMustNotBeArray, nameof(owner));

        return Reference<TValue>.Create(owner, getter);
    }

    /// <summary>
    /// Creates a reference to the value stored in <see cref="StrongBox{T}.Value"/> field.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="box">The container for the value.</param>
    /// <returns>The reference to <see cref="StrongBox{T}.Value"/> field.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="box"/> is <see langword="null"/>.</exception>
    public static unsafe Reference<TValue> Field<TValue>(StrongBox<TValue> box)
        => Create<StrongBox<TValue>, TValue>(box, &GetValueRef<TValue>);

    /// <summary>
    /// Creates a reference to the array element.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="array">One-dimensional array.</param>
    /// <param name="index">The index of the element.</param>
    /// <returns>The reference to the array element.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="array"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than zero or greater than or equal to <paramref name="array"/> length.</exception>
    public static Reference<TValue> ArrayElement<TValue>(TValue[] array, nint index)
    {
        ArgumentNullException.ThrowIfNull(array);

        if ((nuint)index >= (nuint)Intrinsics.GetLength(array))
            throw new ArgumentOutOfRangeException(nameof(index));

        return new(array, index);
    }

    /// <summary>
    /// Allocates the memory location for the value and returns the reference to that location.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The initial value to be placed to the memory location.</param>
    /// <returns>The reference to the allocated memory location.</returns>
    public static unsafe Reference<TValue> Allocate<TValue>(TValue value)
        => Reference<TValue>.Create(new StrongBox<TValue>(value), &GetValueRef<TValue>);

    /// <summary>
    /// Creates a reference to the boxed value.
    /// </summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="boxed">The boxed representation of the value type.</param>
    /// <returns>The reference to the boxed value.</returns>
    /// <exception cref="ArgumentException"><paramref name="boxed"/> is not an instance of <typeparamref name="TValue"/>.</exception>
    public static unsafe Reference<TValue> Unbox<TValue>(object boxed)
        where TValue : struct
    {
        if (boxed is not TValue)
            throw new ArgumentException(ExceptionMessages.BoxedValueTypeExpected<TValue>(), nameof(boxed));

        return Reference<TValue>.Create(boxed, &Unsafe.Unbox<TValue>);
    }

    /// <summary>
    /// Converts the pointer to <see cref="Reference{TValue}"/>.
    /// </summary>
    /// <typeparam name="TValue">The type of the value located at the specified memory location.</typeparam>
    /// <param name="ptr">The pointer representing the address of the value location.</param>
    /// <returns>The reference to the value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is <see langword="null"/>.</exception>
    [CLSCompliant(false)]
    public static unsafe Reference<TValue> FromPointer<TValue>(TValue* ptr)
        where TValue : unmanaged
    {
        if (ptr is null)
            throw new ArgumentNullException(nameof(ptr));

        return new(ptr);
    }

    private static ref TValue GetValueRef<TValue>(StrongBox<TValue> box) => ref box.Value!;
}