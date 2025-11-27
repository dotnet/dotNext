using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext;

using Runtime;
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
public interface ISupplier<in T1, in T2, out TResult> : IFunctional
    where T1 : allows ref struct
    where T2 : allows ref struct
    where TResult : allows ref struct
{
    /// <summary>
    /// Invokes the supplier.
    /// </summary>
    /// <param name="arg1">The first argument.</param>
    /// <param name="arg2">The second argument.</param>
    /// <returns>The value returned by this supplier.</returns>
    TResult Invoke(T1 arg1, T2 arg2);

    /// <inheritdoc/>
    void IFunctional.DynamicInvoke(scoped ref Variant args, int count, scoped Variant result)
        => PrepareInvocation(count, result) = Invoke(
            GetArgument<T1>(ref args, 0),
            GetArgument<T2>(ref args, 1));

    internal static ref TResult PrepareInvocation(int count,
        Variant result,
        [CallerArgumentExpression(nameof(count))]
        string? countArgName = null,
        [CallerArgumentExpression(nameof(result))]
        string? resultArgName = null)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(count, 2, countArgName);
        ArgumentOutOfRangeException.ThrowIfNotEqual(result.IsMutable, true, resultArgName);

        return ref result.Mutable<TResult>();
    }
}

/// <summary>
/// Represents typed function pointer implementing <see cref="ISupplier{T1, T2, TResult}"/>.
/// </summary>
/// <typeparam name="T1">The type of the first argument.</typeparam>
/// <typeparam name="T2">The type of the second argument.</typeparam>
/// <typeparam name="TResult">The type of the result.</typeparam>
/// <remarks>
/// Wraps the function pointer.
/// </remarks>
/// <param name="ptr">The function pointer.</param>
/// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
[StructLayout(LayoutKind.Auto)]
[CLSCompliant(false)]
public readonly unsafe struct Supplier<T1, T2, TResult>(delegate*<T1, T2, TResult> ptr) : ISupplier<T1, T2, TResult>
    where T1 : allows ref struct
    where T2 : allows ref struct
    where TResult : allows ref struct
{
    private readonly delegate*<T1, T2, TResult> ptr = ptr is not null ? ptr : throw new ArgumentNullException(nameof(ptr));

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

    /// <inheritdoc/>
    void IFunctional.DynamicInvoke(scoped ref Variant args, int count, scoped Variant result)
        => ISupplier<T1, T2, TResult>.PrepareInvocation(count, result) = ptr(
            IFunctional.GetArgument<T1>(ref args, 0),
            IFunctional.GetArgument<T2>(ref args, 1));

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
        => Func<T1, T2, TResult>.FromPointer(supplier.ptr);
}

/// <summary>
/// Represents implementation of <see cref="ISupplier{T1, T2, TResult}"/> that delegates
/// invocation to the delegate of type <see cref="Func{T1, T2, TResult}"/>.
/// </summary>
/// <typeparam name="T1">The type of the first argument.</typeparam>
/// <typeparam name="T2">The type of the second argument.</typeparam>
/// <typeparam name="TResult">The type of the result.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly record struct DelegatingSupplier<T1, T2, TResult> : ISupplier<T1, T2, TResult>
    where T1 : allows ref struct
    where T2 : allows ref struct
    where TResult : allows ref struct
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

    /// <inheritdoc/>
    void IFunctional.DynamicInvoke(scoped ref Variant args, int count, scoped Variant result)
        => ISupplier<T1, T2, TResult>.PrepareInvocation(count, result) = func(
            IFunctional.GetArgument<T1>(ref args, 0),
            IFunctional.GetArgument<T2>(ref args, 1));

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
internal readonly struct DelegatingComparer<T>(Comparison<T?> comparison) : IComparer<T>, ISupplier<T?, T?, int>
    where T : allows ref struct
{
    private readonly Comparison<T?> comparison = comparison ?? throw new ArgumentNullException(nameof(comparison));

    /// <inheritdoc />
    int ISupplier<T?, T?, int>.Invoke(T? x, T? y) => comparison(x, y);

    /// <inheritdoc/>
    void IFunctional.DynamicInvoke(scoped ref Variant args, int count, scoped Variant result)
        => ISupplier<T?, T?, int>.PrepareInvocation(count, result) = comparison(
            IFunctional.GetArgument<T?>(ref args, 0),
            IFunctional.GetArgument<T?>(ref args, 1));

    /// <inheritdoc />
    int IComparer<T>.Compare(T? x, T? y) => comparison(x, y);

    public static implicit operator DelegatingComparer<T>(Comparison<T?> comparison) => new(comparison);
}

[StructLayout(LayoutKind.Auto)]
internal readonly unsafe struct ComparerWrapper<T>(delegate*<T?, T?, int> ptr) : IComparer<T>, ISupplier<T?, T?, int>
    where T : allows ref struct
{
    private readonly delegate*<T?, T?, int> ptr = ptr is not null ? ptr : throw new ArgumentNullException(nameof(ptr));

    int ISupplier<T?, T?, int>.Invoke(T? x, T? y) => ptr(x, y);

    int IComparer<T>.Compare(T? x, T? y) => ptr(x, y);

    /// <inheritdoc/>
    void IFunctional.DynamicInvoke(scoped ref Variant args, int count, scoped Variant result)
        => ISupplier<T?, T?, int>.PrepareInvocation(count, result) = ptr(
            IFunctional.GetArgument<T?>(ref args, 0),
            IFunctional.GetArgument<T?>(ref args, 1));

    public static implicit operator ComparerWrapper<T>(delegate*<T?, T?, int> ptr) => new(ptr);
}