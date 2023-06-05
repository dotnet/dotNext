using System.Runtime.InteropServices;

namespace DotNext;

using Runtime.CompilerServices;

/// <summary>
/// Represents functional interface returning arbitrary value and
/// accepting the two arguments.
/// </summary>
/// <remarks>
/// Functional interface can be used to pass
/// some application logic without heap allocation in
/// contrast to regular delegates. Additionally, implementation
/// of functional interface may have encapsulated data acting
/// as closure which is not allocated on the heap.
/// </remarks>
/// <typeparam name="T1">The type of the first argument.</typeparam>
/// <typeparam name="T2">The type of the second argument.</typeparam>
/// <typeparam name="TResult">The type of the result.</typeparam>
public interface ISupplier<in T1, in T2, out TResult> : IFunctional<Func<T1, T2, TResult>>
{
    /// <summary>
    /// Invokes the supplier.
    /// </summary>
    /// <param name="arg1">The first argument.</param>
    /// <param name="arg2">The second argument.</param>
    /// <returns>The value returned by this supplier.</returns>
    TResult Invoke(T1 arg1, T2 arg2);

    /// <inheritdoc />
    Func<T1, T2, TResult> IFunctional<Func<T1, T2, TResult>>.ToDelegate() => Invoke;
}

/// <summary>
/// Represents typed function pointer implementing <see cref="ISupplier{T1, T2, TResult}"/>.
/// </summary>
/// <typeparam name="T1">The type of the first argument.</typeparam>
/// <typeparam name="T2">The type of the second argument.</typeparam>
/// <typeparam name="TResult">The type of the result.</typeparam>
[StructLayout(LayoutKind.Auto)]
[CLSCompliant(false)]
public readonly unsafe struct Supplier<T1, T2, TResult> : ISupplier<T1, T2, TResult>
{
    private readonly delegate*<T1, T2, TResult> ptr;

    /// <summary>
    /// Wraps the function pointer.
    /// </summary>
    /// <param name="ptr">The function pointer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    public Supplier(delegate*<T1, T2, TResult> ptr)
        => this.ptr = ptr is not null ? ptr : throw new ArgumentNullException(nameof(ptr));

    /// <summary>
    /// Gets a value indicating that this function pointer is zero.
    /// </summary>
    public bool IsEmpty => ptr is null;

    /// <inheritdoc />
    TResult ISupplier<T1, T2, TResult>.Invoke(T1 arg1, T2 arg2) => ptr(arg1, arg2);

    /// <summary>
    /// Gets hexadecimal representation of this pointer.
    /// </summary>
    /// <returns>Hexadecimal representation of this pointer.</returns>
    public override string ToString() => new IntPtr(ptr).ToString("X");

    /// <summary>
    /// Converts this supplier to the delegate of type <see cref="Func{T1, T2, TResult}"/>.
    /// </summary>
    /// <returns>The delegate representing the wrapped method.</returns>
    public Func<T1, T2, TResult> ToDelegate() => DelegateHelpers.CreateDelegate<T1, T2, TResult>(ptr);

    /// <summary>
    /// Wraps the function pointer.
    /// </summary>
    /// <param name="ptr">The pointer to the managed method.</param>
    /// <returns>The typed function pointer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    public static implicit operator Supplier<T1, T2, TResult>(delegate*<T1, T2, TResult> ptr)
        => new(ptr);

    /// <summary>
    /// Converts this supplier to the delegate of type <see cref="Func{T1, T2, TResult}"/>.
    /// </summary>
    /// <param name="supplier">The value representing the pointer to the method.</param>
    /// <returns>The delegate representing the wrapped method.</returns>
    public static explicit operator Func<T1, T2, TResult>(Supplier<T1, T2, TResult> supplier)
        => supplier.ToDelegate();
}

/// <summary>
/// Represents implementation of <see cref="ISupplier{T, TResult}"/> interface
/// with the support of closure that is not allocated on the heap.
/// </summary>
/// <typeparam name="TContext">The type describing closure.</typeparam>
/// <typeparam name="T1">The type of the first argument.</typeparam>
/// <typeparam name="T2">The type of the second argument.</typeparam>
/// <typeparam name="TResult">The type of the result.</typeparam>
[StructLayout(LayoutKind.Auto)]
[CLSCompliant(false)]
public readonly unsafe struct SupplierClosure<TContext, T1, T2, TResult> : ISupplier<T1, T2, TResult>
{
    private readonly delegate*<in TContext, T1, T2, TResult> ptr;
    private readonly TContext context;

    /// <summary>
    /// Wraps the function pointer.
    /// </summary>
    /// <param name="ptr">The function pointer.</param>
    /// <param name="context">The context to be passed to the function pointer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    public SupplierClosure(TContext context, delegate*<in TContext, T1, T2, TResult> ptr)
    {
        this.ptr = ptr is not null ? ptr : throw new ArgumentNullException(nameof(ptr));
        this.context = context;
    }

    /// <summary>
    /// Gets a value indicating that this function pointer is zero.
    /// </summary>
    public bool IsEmpty => ptr is null;

    /// <inheritdoc />
    TResult ISupplier<T1, T2, TResult>.Invoke(T1 arg1, T2 arg2) => ptr(in context, arg1, arg2);
}

/// <summary>
/// Represents implementation of <see cref="ISupplier{T1, T2, TResult}"/> that delegates
/// invocation to the delegate of type <see cref="Func{T1, T2, TResult}"/>.
/// </summary>
/// <typeparam name="T1">The type of the first argument.</typeparam>
/// <typeparam name="T2">The type of the second argument.</typeparam>
/// <typeparam name="TResult">The type of the result.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly record struct DelegatingSupplier<T1, T2, TResult> : ISupplier<T1, T2, TResult>, IEquatable<DelegatingSupplier<T1, T2, TResult>>
{
    private readonly Func<T1, T2, TResult> func;

    /// <summary>
    /// Wraps the delegate instance.
    /// </summary>
    /// <param name="func">The delegate instance.</param>
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
    public DelegatingSupplier(Func<T1, T2, TResult> func)
        => this.func = func ?? throw new ArgumentNullException(nameof(func));

    /// <summary>
    /// Gets a value indicating that the underlying delegate is <see langword="null"/>.
    /// </summary>
    public bool IsEmpty => func is null;

    /// <inheritdoc />
    TResult ISupplier<T1, T2, TResult>.Invoke(T1 arg1, T2 arg2) => func(arg1, arg2);

    /// <inheritdoc />
    Func<T1, T2, TResult> IFunctional<Func<T1, T2, TResult>>.ToDelegate() => func;

    /// <inheritdoc />
    public override string? ToString() => func?.ToString();

    /// <summary>
    /// Wraps the delegate instance.
    /// </summary>
    /// <param name="func">The delegate instance.</param>
    /// <returns>The supplier represented by the delegate.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
    public static implicit operator DelegatingSupplier<T1, T2, TResult>(Func<T1, T2, TResult> func) => new(func);
}

[StructLayout(LayoutKind.Auto)]
internal readonly struct DelegatingComparer<T> : IComparer<T>, ISupplier<T?, T?, int>, IFunctional<Comparison<T?>>
{
    private readonly Comparison<T?> comparison;

    internal DelegatingComparer(Comparison<T?> comparison)
        => this.comparison = comparison ?? throw new ArgumentNullException(nameof(comparison));

    /// <inheritdoc />
    int ISupplier<T?, T?, int>.Invoke(T? x, T? y) => comparison(x, y);

    /// <inheritdoc />
    Func<T?, T?, int> IFunctional<Func<T?, T?, int>>.ToDelegate() => comparison.ChangeType<Func<T?, T?, int>>();

    /// <inheritdoc />
    Comparison<T?> IFunctional<Comparison<T?>>.ToDelegate() => comparison;

    /// <inheritdoc />
    int IComparer<T>.Compare(T? x, T? y) => comparison(x, y);

    public static implicit operator DelegatingComparer<T>(Comparison<T?> comparison) => new(comparison);
}

[StructLayout(LayoutKind.Auto)]
internal readonly unsafe struct ComparerWrapper<T> : IComparer<T>, ISupplier<T?, T?, int>
{
    private readonly delegate*<T?, T?, int> ptr;

    internal ComparerWrapper(delegate*<T?, T?, int> ptr)
        => this.ptr = ptr is not null ? ptr : throw new ArgumentNullException(nameof(ptr));

    int ISupplier<T?, T?, int>.Invoke(T? x, T? y) => ptr(x, y);

    int IComparer<T>.Compare(T? x, T? y) => ptr(x, y);

    public static implicit operator ComparerWrapper<T>(delegate*<T?, T?, int> ptr) => new(ptr);
}