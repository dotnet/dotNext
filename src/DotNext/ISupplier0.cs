using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using RuntimeHelpers = System.Runtime.CompilerServices.RuntimeHelpers;

namespace DotNext
{
    using FunctionalInterfaceAttribute = Runtime.CompilerServices.FunctionalInterfaceAttribute;

    /// <summary>
    /// Represents functional interface returning arbitrary value.
    /// </summary>
    /// <remarks>
    /// Functional interface can be used to pass
    /// some application logic without heap allocation in
    /// contrast to regulat delegates. Additionally, implementation
    /// of functional interface may have encapsulated data acting
    /// as closure which is not allocated on the heap.
    /// </remarks>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    [FunctionalInterface]
    public interface ISupplier<out TResult>
    {
        /// <summary>
        /// Invokes the supplier.
        /// </summary>
        /// <returns>The value returned by this supplier.</returns>
        TResult Invoke();
    }

    /// <summary>
    /// Represents typed function pointer implementing <see cref="ISupplier{TResult}"/>.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    [CLSCompliant(false)]
    public readonly unsafe struct Supplier<TResult> : ISupplier<TResult>
    {
        private readonly delegate*<TResult> ptr;

        /// <summary>
        /// Wraps the function pointer.
        /// </summary>
        /// <param name="ptr">The function pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        public Supplier(delegate*<TResult> ptr)
            => this.ptr = ptr == null ? throw new ArgumentNullException(nameof(ptr)) : ptr;

        /// <summary>
        /// Gets a value indicating that this function pointer is zero.
        /// </summary>
        public bool IsEmpty => ptr == null;

        /// <inheritdoc />
        TResult ISupplier<TResult>.Invoke() => ptr();

        /// <summary>
        /// Converts this supplier to the delegate of type <see cref="Func{TResult}"/>.
        /// </summary>
        /// <returns>The delegate representing the wrapped method.</returns>
        public Func<TResult> ToDelegate() => DelegateHelpers.CreateDelegate<TResult>(ptr);

        /// <summary>
        /// Gets hexadecimal representation of this pointer.
        /// </summary>
        /// <returns>Hexadecimal representation of this pointer.</returns>
        public override string ToString() => new IntPtr(ptr).ToString("X");

        /// <summary>
        /// Wraps the function pointer.
        /// </summary>
        /// <param name="ptr">The pointer to the managed method.</param>
        /// <returns>The typed function pointer.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        public static implicit operator Supplier<TResult>(delegate*<TResult> ptr) => new(ptr);

        /// <summary>
        /// Converts this supplier to the delegate of type <see cref="Func{TResult}"/>.
        /// </summary>
        /// <param name="supplier">The value representing the pointer to the method.</param>
        /// <returns>The delegate representing the wrapped method.</returns>
        public static explicit operator Func<TResult>(Supplier<TResult> supplier) => supplier.ToDelegate();
    }

    /// <summary>
    /// Represents implementation of <see cref="ISupplier{TResult}"/>
    /// that acts as activator of type with public parameterless constructor.
    /// </summary>
    /// <typeparam name="T">The type with public parameterless constructor.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Activator<T> : ISupplier<T>
        where T : new()
    {
        /// <inheritdoc />
        T ISupplier<T>.Invoke() => new();
    }

    /// <summary>
    /// Represents constant value supplier.
    /// </summary>
    /// <typeparam name="T">The type of the value to supply.</typeparam>
    public readonly struct ValueSupplier<T> : ISupplier<T>
    {
        private readonly T value;

        /// <summary>
        /// Creates a new wrapper for the value.
        /// </summary>
        /// <param name="value">The value to wrap.</param>
        public ValueSupplier(T value) => this.value = value;

        /// <inheritdoc />
        T ISupplier<T>.Invoke() => value;

        /// <summary>
        /// Creates constant value supplier.
        /// </summary>
        /// <param name="value">The value to wrap.</param>
        /// <returns>The wrapper over the value.</returns>
        public static implicit operator ValueSupplier<T>(T value) => new(value);
    }

    /// <summary>
    /// Represents implementation of <see cref="ISupplier{TResult}"/> interface
    /// with the support of closure that is not allocated on the heap.
    /// </summary>
    /// <typeparam name="TContext">The type describing closure.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    [CLSCompliant(false)]
    public readonly unsafe struct SupplierClosure<TContext, TResult> : ISupplier<TResult>
    {
        private readonly delegate*<in TContext, TResult> ptr;
        private readonly TContext context;

        /// <summary>
        /// Wraps the function pointer.
        /// </summary>
        /// <param name="ptr">The function pointer.</param>
        /// <param name="context">The context to be passed to the function pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        public SupplierClosure(delegate*<in TContext, TResult> ptr, TContext context)
        {
            this.ptr = ptr == null ? throw new ArgumentNullException(nameof(ptr)) : ptr;
            this.context = context;
        }

        /// <summary>
        /// Gets a value indicating that this function pointer is zero.
        /// </summary>
        public bool IsEmpty => ptr == null;

        /// <inheritdoc />
        TResult ISupplier<TResult>.Invoke() => ptr(in context);
    }

    /// <summary>
    /// Represents implementation of <see cref="ISupplier{TResult}"/> that delegates
    /// invocation to the delegate of type <see cref="Func{TResult}"/>.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct DelegatingSupplier<TResult> : ISupplier<TResult>, IEquatable<DelegatingSupplier<TResult>>
    {
        private readonly Func<TResult> func;

        /// <summary>
        /// Wraps the delegate instance.
        /// </summary>
        /// <param name="func">The delegate instance.</param>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public DelegatingSupplier(Func<TResult> func)
            => this.func = func ?? throw new ArgumentNullException(nameof(func));

        /// <summary>
        /// Gets a value indicating that the underlying delegate is <see langword="null"/>.
        /// </summary>
        public bool IsEmpty => func is null;

        /// <inheritdoc />
        TResult ISupplier<TResult>.Invoke() => func();

        /// <summary>
        /// Determines whether this object contains the same delegate instance as the specified object.
        /// </summary>
        /// <param name="other">The object to compare.</param>
        /// <returns><see langword="true"/> if this object contains the same delegate instance as <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public bool Equals(DelegatingSupplier<TResult> other)
            => ReferenceEquals(func, other.func);

        /// <summary>
        /// Determines whether this object contains the same delegate instance as the specified object.
        /// </summary>
        /// <param name="other">The object to compare.</param>
        /// <returns><see langword="true"/> if this object contains the same delegate instance as <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public override bool Equals([NotNullWhen(true)] object? other) => other is DelegatingSupplier<TResult> supplier && Equals(supplier);

        /// <summary>
        /// Gets the hash code representing identity of the stored delegate instance.
        /// </summary>
        /// <returns>The hash code representing identity of the stored delegate instance.</returns>
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(func);

        /// <summary>
        /// Wraps the delegate instance.
        /// </summary>
        /// <param name="func">The delegate instance.</param>
        /// <returns>The supplier represented by the delegate.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public static implicit operator DelegatingSupplier<TResult>(Func<TResult> func) => new(func);

        /// <summary>
        /// Determines whether the two objects contain references to the same delegate instance.
        /// </summary>
        /// <param name="x">The first object to compare.</param>
        /// <param name="y">The second object to compare.</param>
        /// <returns><see langword="true"/> if the both objects contain references the same delegate instance; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(DelegatingSupplier<TResult> x, DelegatingSupplier<TResult> y)
            => x.Equals(y);

        /// <summary>
        /// Determines whether the two objects contain references to the different delegate instances.
        /// </summary>
        /// <param name="x">The first object to compare.</param>
        /// <param name="y">The second object to compare.</param>
        /// <returns><see langword="true"/> if the both objects contain references the different delegate instances; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(DelegatingSupplier<TResult> x, DelegatingSupplier<TResult> y)
            => !x.Equals(y);
    }
}