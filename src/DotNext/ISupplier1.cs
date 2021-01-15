using System;
using System.Runtime.InteropServices;
using RuntimeHelpers = System.Runtime.CompilerServices.RuntimeHelpers;

namespace DotNext
{
    using FunctionalInterfaceAttribute = Runtime.CompilerServices.FunctionalInterfaceAttribute;

    /// <summary>
    /// Represents functional interface returning arbitrary value and
    /// accepting the single argument.
    /// </summary>
    /// <remarks>
    /// Functional interface can be used to pass
    /// some application logic without heap allocation in
    /// contrast to regulat delegates. Additionally, implementation
    /// of functional interface may have encapsulated data acting
    /// as closure which is not allocated on the heap.
    /// </remarks>
    /// <typeparam name="T">The type of the argument.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    [FunctionalInterface]
    public interface ISupplier<in T, out TResult>
    {
        /// <summary>
        /// Invokes the supplier.
        /// </summary>
        /// <param name="arg">The first argument.</param>
        /// <returns>The value returned by this supplier.</returns>
        TResult Invoke(T arg);
    }

    /// <summary>
    /// Represents typed function pointer implementing <see cref="ISupplier{T, TResult}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the argument.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    [CLSCompliant(false)]
    public readonly unsafe struct Supplier<T, TResult> : ISupplier<T, TResult>, IEquatable<Supplier<T, TResult>>
    {
        private readonly delegate*<T, TResult> ptr;

        /// <summary>
        /// Wraps the function pointer.
        /// </summary>
        /// <param name="ptr">The function pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        public Supplier(delegate*<T, TResult> ptr)
            => this.ptr = ptr == null ? throw new ArgumentNullException(nameof(ptr)) : ptr;

        /// <summary>
        /// Gets a value indicating that this function pointer is zero.
        /// </summary>
        public bool IsEmpty => ptr == null;

        /// <inheritdoc />
        TResult ISupplier<T, TResult>.Invoke(T arg) => ptr(arg);

        /// <summary>
        /// Determines whether the two objects contain the same pointer.
        /// </summary>
        /// <param name="other">The object to be compared.</param>
        /// <returns><see langword="true"/> if this object contains the same pointer as the specified object.</returns>
        public bool Equals(Supplier<T, TResult> other) => ptr == other.ptr;

        /// <summary>
        /// Determines whether the two objects contain the same pointer.
        /// </summary>
        /// <param name="other">The object to be compared.</param>
        /// <returns><see langword="true"/> if this object contains the same pointer as the specified object.</returns>
        public override bool Equals(object? other) => other is Supplier<T, TResult> supplier && Equals(supplier);

        /// <summary>
        /// Gets the hash code of this function pointer.
        /// </summary>
        /// <returns>The hash code of the function pointer.</returns>
        public override int GetHashCode() => Runtime.Intrinsics.PointerHashCode(ptr);

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
        public static implicit operator Supplier<T, TResult>(delegate*<T, TResult> ptr)
            => new (ptr);

        /// <summary>
        /// Determines whether the two objects contain the same pointer.
        /// </summary>
        /// <param name="x">The first object to compare.</param>
        /// <param name="y">The second object to compare.</param>
        /// <see langword="true"/> if both objects contain the same pointer; otherwise, <see langword="false"/>.
        public static bool operator ==(Supplier<T, TResult> x, Supplier<T, TResult> y)
            => x.Equals(y);

        /// <summary>
        /// Determines whether the two objects contain the different pointers.
        /// </summary>
        /// <param name="x">The first object to compare.</param>
        /// <param name="y">The second object to compare.</param>
        /// <see langword="true"/> if both objects contain the different pointers; otherwise, <see langword="false"/>.
        public static bool operator !=(Supplier<T, TResult> x, Supplier<T, TResult> y)
            => !x.Equals(y);
    }

    /// <summary>
    /// Represents implementation of <see cref="ISupplier{T, TResult}"/> interface
    /// with the support of closure that is not allocated on the heap.
    /// </summary>
    /// <typeparam name="TContext">The type describing closure.</typeparam>
    /// <typeparam name="T">The type of the argument.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    [CLSCompliant(false)]
    public readonly unsafe struct SupplierClosure<TContext, T, TResult> : ISupplier<T, TResult>
    {
        private readonly delegate*<in TContext, T, TResult> ptr;
        private readonly TContext context;

        /// <summary>
        /// Wraps the function pointer.
        /// </summary>
        /// <param name="ptr">The function pointer.</param>
        /// <param name="context">The context to be passed to the function pointer.</param>
        /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
        public SupplierClosure(delegate*<in TContext, T, TResult> ptr, TContext context)
        {
            this.ptr = ptr == null ? throw new ArgumentNullException(nameof(ptr)) : ptr;
            this.context = context;
        }

        /// <summary>
        /// Gets a value indicating that this function pointer is zero.
        /// </summary>
        public bool IsEmpty => ptr == null;

        /// <inheritdoc />
        TResult ISupplier<T, TResult>.Invoke(T arg) => ptr(in context, arg);
    }

    /// <summary>
    /// Represents implementation of <see cref="ISupplier{T, TResult}"/> that delegates
    /// invocation to the delegate of type <see cref="Func{T, TResult}"/>
    /// </summary>
    /// <typeparam name="T">The type of the argument.</typeparam>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct DelegatingSupplier<T, TResult> : ISupplier<T, TResult>, IEquatable<DelegatingSupplier<T, TResult>>
    {
        private readonly Func<T, TResult> func;

        /// <summary>
        /// Wraps the delegate instance.
        /// </summary>
        /// <param name="func">The delegate instance.</param>
        /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
        public DelegatingSupplier(Func<T, TResult> func)
            => this.func = func ?? throw new ArgumentNullException(nameof(func));

        /// <summary>
        /// Gets a value indicating that the underlying delegate is <see langword="null"/>.
        /// </summary>
        public bool IsEmpty => func is null;

        /// <inheritdoc />
        TResult ISupplier<T, TResult>.Invoke(T arg) => func(arg);

        /// <summary>
        /// Determines whether this object contains the same delegate instance as the specified object.
        /// </summary>
        /// <param name="other">The object to compare.</param>
        /// <returns><see langword="true"/> if this object contains the same delegate instance as <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public bool Equals(DelegatingSupplier<T, TResult> other)
            => ReferenceEquals(func, other.func);

        /// <summary>
        /// Determines whether this object contains the same delegate instance as the specified object.
        /// </summary>
        /// <param name="other">The object to compare.</param>
        /// <returns><see langword="true"/> if this object contains the same delegate instance as <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object? other) => other is DelegatingSupplier<T, TResult> supplier && Equals(supplier);

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
        public static implicit operator DelegatingSupplier<T, TResult>(Func<T, TResult> func)
            => new (func);

        /// <summary>
        /// Determines whether the two objects contain references to the same delegate instance.
        /// </summary>
        /// <param name="x">The first object to compare.</param>
        /// <param name="y">The second object to compare.</param>
        /// <returns><see langword="true"/> if the both objects contain references the same delegate instance; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(DelegatingSupplier<T, TResult> x, DelegatingSupplier<T, TResult> y)
            => x.Equals(y);

        /// <summary>
        /// Determines whether the two objects contain references to the different delegate instances.
        /// </summary>
        /// <param name="x">The first object to compare.</param>
        /// <param name="y">The second object to compare.</param>
        /// <returns><see langword="true"/> if the both objects contain references the different delegate instances; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(DelegatingSupplier<T, TResult> x, DelegatingSupplier<T, TResult> y)
            => !x.Equals(y);
    }

    [StructLayout(LayoutKind.Auto)]
    internal readonly struct DelegatingPredicate<T> : ISupplier<T, bool>
    {
        private readonly Predicate<T> predicate;

        internal DelegatingPredicate(Predicate<T> predicate)
            => this.predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));

        /// <inheritdoc />
        bool ISupplier<T, bool>.Invoke(T arg) => predicate(arg);

        public static implicit operator DelegatingPredicate<T>(Predicate<T> predicate)
            => new (predicate);
    }

    [StructLayout(LayoutKind.Auto)]
    internal readonly struct DelegatingConverter<TInput, TOutput> : ISupplier<TInput, TOutput>
    {
        private readonly Converter<TInput, TOutput> converter;

        internal DelegatingConverter(Converter<TInput, TOutput> converter)
            => this.converter = converter ?? throw new ArgumentNullException(nameof(converter));

        /// <inheritdoc />
        TOutput ISupplier<TInput, TOutput>.Invoke(TInput arg) => converter(arg);

        public static implicit operator DelegatingConverter<TInput, TOutput>(Converter<TInput, TOutput> converter)
            => new (converter);
    }
}