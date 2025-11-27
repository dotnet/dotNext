using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext;

using Runtime;
using Runtime.CompilerServices;

/// <summary>
/// Represents functional interface returning arbitrary value and
/// accepting the single argument.
/// </summary>
/// <remarks>
/// Functional interface can be used to pass
/// some application logic without heap allocation in
/// contrast to regular delegates. Additionally, implementation
/// of functional interface may have encapsulated data acting
/// as closure which is not allocated on the heap.
/// </remarks>
/// <typeparam name="T">The type of the argument.</typeparam>
/// <typeparam name="TResult">The type of the result.</typeparam>
public interface ISupplier<in T, out TResult> : IFunctional
    where T : allows ref struct
    where TResult : allows ref struct
{
    /// <summary>
    /// Invokes the supplier.
    /// </summary>
    /// <param name="arg">The first argument.</param>
    /// <returns>The value returned by this supplier.</returns>
    TResult Invoke(T arg);

    /// <inheritdoc/>
    void IFunctional.DynamicInvoke(scoped ref Variant args, int count, scoped Variant result)
        => PrepareInvocation(count, result) = Invoke(args.ReadOnly<T>());

    internal static ref TResult PrepareInvocation(int count,
        Variant result,
        [CallerArgumentExpression(nameof(count))]
        string? countArgName = null,
        [CallerArgumentExpression(nameof(result))]
        string? resultArgName = null)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(count, 1, countArgName);
        ArgumentOutOfRangeException.ThrowIfNotEqual(result.IsMutable, true, resultArgName);

        return ref result.Mutable<TResult>();
    }
}

/// <summary>
/// Represents typed function pointer implementing <see cref="ISupplier{T, TResult}"/>.
/// </summary>
/// <typeparam name="T">The type of the argument.</typeparam>
/// <typeparam name="TResult">The type of the result.</typeparam>
/// <remarks>
/// Wraps the function pointer.
/// </remarks>
/// <param name="ptr">The function pointer.</param>
/// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
[StructLayout(LayoutKind.Auto)]
[CLSCompliant(false)]
public readonly unsafe struct Supplier<T, TResult>(delegate*<T, TResult> ptr) : ISupplier<T, TResult>
    where T : allows ref struct
    where TResult : allows ref struct
{
    private readonly delegate*<T, TResult> ptr = ptr is not null ? ptr : throw new ArgumentNullException(nameof(ptr));

    /// <summary>
    /// Gets a value indicating that this function pointer is zero.
    /// </summary>
    public bool IsEmpty => ptr is null;

    /// <inheritdoc />
    TResult ISupplier<T, TResult>.Invoke(T arg) => ptr(arg);

    /// <inheritdoc/>
    void IFunctional.DynamicInvoke(scoped ref Variant args, int count, scoped Variant result)
        => ISupplier<T, TResult>.PrepareInvocation(count, result) = ptr(args.ReadOnly<T>());

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
    public static implicit operator Supplier<T, TResult>(delegate*<T, TResult> ptr) => new(ptr);
    
    /// <summary>
    /// Converts this supplier to the delegate of type <see cref="Func{T, TResult}"/>.
    /// </summary>
    /// <param name="supplier">The value representing the pointer to the method.</param>
    /// <returns>The delegate representing the wrapped method.</returns>
    public static explicit operator Func<T, TResult>(Supplier<T, TResult> supplier)
        => Func<T, TResult>.FromPointer(supplier.ptr);
}

/// <summary>
/// Represents implementation of <see cref="ISupplier{T, TResult}"/> that delegates
/// invocation to the delegate of type <see cref="Func{T, TResult}"/>.
/// </summary>
/// <typeparam name="T">The type of the argument.</typeparam>
/// <typeparam name="TResult">The type of the result.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly record struct DelegatingSupplier<T, TResult> : ISupplier<T, TResult>
    where T :allows ref struct
    where TResult : allows ref struct
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

    /// <inheritdoc/>
    void IFunctional.DynamicInvoke(scoped ref Variant args, int count, scoped Variant result)
        => ISupplier<T, TResult>.PrepareInvocation(count, result) = func(args.ReadOnly<T>());

    /// <inheritdoc />
    public override string? ToString() => func?.ToString();

    /// <summary>
    /// Wraps the delegate instance.
    /// </summary>
    /// <param name="func">The delegate instance.</param>
    /// <returns>The supplier represented by the delegate.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
    public static implicit operator DelegatingSupplier<T, TResult>(Func<T, TResult> func)
        => new(func);
}

[StructLayout(LayoutKind.Auto)]
internal readonly struct DelegatingPredicate<T>(Predicate<T> predicate) : ISupplier<T, bool>
    where T : allows ref struct
{
    /// <inheritdoc />
    bool ISupplier<T, bool>.Invoke(T arg) => predicate(arg);

    /// <inheritdoc/>
    void IFunctional.DynamicInvoke(scoped ref Variant args, int count, scoped Variant result)
        => ISupplier<T, bool>.PrepareInvocation(count, result) = predicate(args.ReadOnly<T>());

    public static implicit operator DelegatingPredicate<T>(Predicate<T> predicate)
        => new(predicate);
}

[StructLayout(LayoutKind.Auto)]
internal readonly struct DelegatingConverter<TInput, TOutput>(Converter<TInput, TOutput> converter) : ISupplier<TInput, TOutput>
    where TInput : allows ref struct
    where TOutput : allows ref struct
{
    private readonly Converter<TInput, TOutput> converter = converter ?? throw new ArgumentNullException(nameof(converter));

    /// <inheritdoc />
    TOutput ISupplier<TInput, TOutput>.Invoke(TInput arg) => converter(arg);

    /// <inheritdoc/>
    void IFunctional.DynamicInvoke(scoped ref Variant args, int count, scoped Variant result)
        => ISupplier<TInput, TOutput>.PrepareInvocation(count, result) = converter(args.ReadOnly<TInput>());

    public static implicit operator DelegatingConverter<TInput, TOutput>(Converter<TInput, TOutput> converter)
        => new(converter);
}