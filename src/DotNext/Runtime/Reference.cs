using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Runtime
{
    internal static class PointerTypeSentinel
    {
        internal static readonly object Instance = new();
    }

    /// <summary>
    /// Provides read/write access to a static or an instance field.
    /// </summary>
    /// <remarks>
    /// This type encapsulates the reference to the memory location where the value is stored.
    /// The handle itself can be used in async context and stored in a field or a regular class in contrast to <c>ref</c> keyword
    /// in C# and <c>ref</c>-structs.
    /// </remarks>
    /// <typeparam name="TValue">The type of the field.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly unsafe struct ValueHandle<TValue>
    {
        /*
         * if owner is null then accessor is of type delegate*<ref TValue>,
         * if owner is TValue[] then accessor is of type nint;
         * if owner is the same as PointerTypeSentinel.Instance then accessor is of type TValue*;
         * otherwise, access is of type delegate*<object, ref TValue>.
         */
        private readonly void* accessor;
        private readonly object? owner;

        private ValueHandle(object owner, delegate*<object, ref TValue> accessor)
        {
            Debug.Assert(owner is not null);
            Debug.Assert(accessor != null);

            this.owner = owner;
            this.accessor = accessor;
        }

        internal ValueHandle(delegate*<ref TValue> accessor)
        {
            Debug.Assert(accessor != null);

            owner = null;
            this.accessor = accessor;
        }

        internal ValueHandle(TValue[] array, nint index)
        {
            Debug.Assert(array is not null);

            owner = array;
            accessor = (void*)index;
        }

        internal ValueHandle(void* pointer)
        {
            Debug.Assert(pointer != null);

            owner = PointerTypeSentinel.Instance;
            accessor = pointer;
        }

        internal static ValueHandle<TValue> Create<TOwner>(TOwner owner, delegate*<TOwner, ref TValue> accessor)
            where TOwner : class
            => new(owner, (delegate*<object, ref TValue>)accessor);

        /// <summary>
        /// Gets a value indicating that this handle is valid.
        /// </summary>
        public bool IsValid => accessor != null || owner is TValue[];

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
                else if (accessor == null)
                {
                    // leave getter
                }
                else if (owner is null)
                {
                    result = ref ((delegate*<ref TValue>)accessor)();
                }
                else if (ReferenceEquals(owner, PointerTypeSentinel.Instance))
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
        /// Gets a reference to a memory location where the value is stored.
        /// </summary>
        public ref TValue Value
        {
            get
            {
                ref TValue result = ref RawValue;

                if (Unsafe.IsNullRef(ref result))
                    throw new NullReferenceException();

                return ref result;
            }
        }

        /// <summary>
        /// Gets a span with the single element representing the underlying value.
        /// </summary>
        /// <remarks>
        /// The returned span is always of size 1 for the valid handle. If this handle is invalid
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
    /// Provides factory methods for creating handles.
    /// </summary>
    public static class ValueHandle
    {
        /// <summary>
        /// Creates a handle to a value stored as a static field.
        /// </summary>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="getter">The function providing the location of the value in the memory.</param>
        /// <returns>The value handle.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="getter"/> is <see langword="null"/>.</exception>
        [CLSCompliant(false)]
        public static unsafe ValueHandle<TValue> Create<TValue>(delegate*<ref TValue> getter)
        {
            if (getter == null)
                throw new ArgumentNullException(nameof(getter));

            return new(getter);
        }

        /// <summary>
        /// Creates a handle to a value stored in the object.
        /// </summary>
        /// <typeparam name="TOwner">The type of the object that stores the value.</typeparam>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="owner">The object that stores the value.</param>
        /// <param name="getter">The function providing the location of the value in the memory.</param>
        /// <returns>The handle providing access to instance field.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="owner"/> is <see langword="null"/>;
        /// or <paramref name="getter"/> is <see langword="null"/>.
        /// </exception>
        [CLSCompliant(false)]
        public static unsafe ValueHandle<TValue> Create<TOwner, TValue>(TOwner owner, delegate*<TOwner, ref TValue> getter)
            where TOwner : class
        {
            if (owner is null)
                throw new ArgumentNullException(nameof(owner));

            if (getter == null)
                throw new ArgumentNullException(nameof(getter));

            return ValueHandle<TValue>.Create(owner, getter);
        }

        /// <summary>
        /// Creates a handle to the value stored in <see cref="StrongBox{T}.Value"/> field.
        /// </summary>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="box">The container for the value.</param>
        /// <returns>The handle providing access to <see cref="StrongBox{T}.Value"/> field.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="box"/> is <see langword="null"/>.</exception>
        public static unsafe ValueHandle<TValue> Field<TValue>(StrongBox<TValue> box)
            => Create<StrongBox<TValue>, TValue>(box, &GetValueRef<TValue>);

        /// <summary>
        /// Creates a handle to the array element.
        /// </summary>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="array">One-dimensional array.</param>
        /// <param name="index">The index of the element.</param>
        /// <returns>The handle providing access to the array element.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="array"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than zero or greater than or equal to <paramref name="array"/> length.</exception>
        public static ValueHandle<TValue> ArrayElement<TValue>(TValue[] array, nint index)
        {
            if (array is null)
                throw new ArgumentNullException(nameof(array));

            if (index < 0 || index >= Intrinsics.GetLength(array))
                throw new ArgumentOutOfRangeException(nameof(index));

            return new(array, index);
        }

        /// <summary>
        /// Allocates the memory location for the value and returns the handle to that location.
        /// </summary>
        /// <typeparam name="TValue">The type of the value.</typeparam>
        /// <param name="value">The initial value to be placed to the memory location.</param>
        /// <returns>The handle providing access to the memory location.</returns>
        public static unsafe ValueHandle<TValue> Allocate<TValue>(TValue value)
            => ValueHandle<TValue>.Create(new StrongBox<TValue>(value), &GetValueRef<TValue>);

        /// <summary>
        /// Creates a handle for the boxed value stored in the heap.
        /// </summary>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <param name="boxed">The boxed representation of the value type.</param>
        /// <returns>The handle that stores memory location of the boxed value.</returns>
        /// <exception cref="ArgumentException"><paramref name="boxed"/> is not an instance of <typeparamref name="TValue"/>.</exception>
        public static unsafe ValueHandle<TValue> Unbox<TValue>(object boxed)
            where TValue : struct
        {
            if (boxed is not TValue)
                throw new ArgumentException(ExceptionMessages.BoxedValueTypeExpected<TValue>(), nameof(boxed));

            return ValueHandle<TValue>.Create(boxed, &Unsafe.Unbox<TValue>);
        }

        /// <summary>
        /// Wraps the pointer.
        /// </summary>
        /// <typeparam name="TValue">The type of the value located at the specified memory location.</typeparam>
        /// <param name="ptr">The pointer representing the address of the value location.</param>
        /// <returns>The handle wrapping the pointer.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is <see langword="null"/>.</exception>
        [CLSCompliant(false)]
        public static unsafe ValueHandle<TValue> FromPointer<TValue>(TValue* ptr)
            where TValue : unmanaged
        {
            if (ptr == null)
                throw new ArgumentNullException(nameof(ptr));

            return new(ptr);
        }

        private static ref TValue GetValueRef<TValue>(StrongBox<TValue> box) => ref box.Value!;
    }
}