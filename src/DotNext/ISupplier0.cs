using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext;

using Runtime;
using Runtime.CompilerServices;

/// <summary>
/// Represents functional interface returning arbitrary value.
/// </summary>
/// <remarks>
/// Functional interface can be used to pass
/// some application logic without heap allocation in
/// contrast to regular delegates. Additionally, implementation
/// of functional interface may have encapsulated data acting
/// as closure which is not allocated on the heap.
/// </remarks>
/// <typeparam name="TResult">The type of the result.</typeparam>
public interface ISupplier<out TResult> : IFunctional
    where TResult : allows ref struct
{
    /// <summary>
    /// Invokes the supplier.
    /// </summary>
    /// <returns>The value returned by this supplier.</returns>
    TResult Invoke();

    /// <inheritdoc/>
    void IFunctional.DynamicInvoke(ref readonly Variant args, int count, scoped Variant result)
        => PrepareInvocation(count, result) = Invoke();

    internal static ref TResult PrepareInvocation(int count,
        Variant result,
        [CallerArgumentExpression(nameof(count))]
        string? countArgName = null,
        [CallerArgumentExpression(nameof(result))]
        string? resultArgName = null)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(count, 0, countArgName);
        ArgumentOutOfRangeException.ThrowIfNotEqual(result.IsMutable, true, resultArgName);

        return ref result.Mutable<TResult>();
    }
}

/// <summary>
/// Represents typed function pointer implementing <see cref="ISupplier{TResult}"/>.
/// </summary>
/// <typeparam name="TResult">The type of the result.</typeparam>
/// <remarks>
/// Wraps the function pointer.
/// </remarks>
/// <param name="ptr">The function pointer.</param>
/// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
[StructLayout(LayoutKind.Auto)]
[CLSCompliant(false)]
public readonly unsafe struct Supplier<TResult>(delegate*<TResult> ptr) : ISupplier<TResult>
    where TResult : allows ref struct
{
    private readonly delegate*<TResult> ptr = ptr is not null ? ptr : throw new ArgumentNullException(nameof(ptr));

    /// <summary>
    /// Gets a value indicating that this function pointer is zero.
    /// </summary>
    public bool IsEmpty => ptr is null;

    /// <inheritdoc />
    TResult ISupplier<TResult>.Invoke() => ptr();

    /// <inheritdoc/>
    void IFunctional.DynamicInvoke(ref readonly Variant args, int count, scoped Variant result)
        => ISupplier<TResult>.PrepareInvocation(count, result) = ptr();

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
    public static explicit operator Func<TResult>(Supplier<TResult> supplier)
        => Func<TResult>.FromPointer(supplier.ptr);
}

/// <summary>
/// Represents implementation of <see cref="ISupplier{TResult}"/>
/// that acts as activator of type with public parameterless constructor.
/// </summary>
/// <typeparam name="T">The type with public parameterless constructor.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly struct Activator<T> : ISupplier<T>
    where T : new(), allows ref struct
{
    /// <inheritdoc />
    T ISupplier<T>.Invoke() => new();

    /// <inheritdoc/>
    void IFunctional.DynamicInvoke(ref readonly Variant args, int count, scoped Variant result)
        => ISupplier<T>.PrepareInvocation(count, result) = new();
}

/// <summary>
/// Represents constant value supplier.
/// </summary>
/// <typeparam name="T">The type of the value to supply.</typeparam>
/// <remarks>
/// Creates a new wrapper for the value.
/// </remarks>
/// <param name="value">The value to wrap.</param>
public readonly struct ValueSupplier<T>(T value) : ISupplier<T>
{
    /// <inheritdoc />
    T ISupplier<T>.Invoke() => value;

    /// <inheritdoc/>
    void IFunctional.DynamicInvoke(ref readonly Variant args, int count, scoped Variant result)
        => ISupplier<T>.PrepareInvocation(count, result) = value;

    /// <summary>
    /// Creates constant value supplier.
    /// </summary>
    /// <param name="value">The value to wrap.</param>
    /// <returns>The wrapper over the value.</returns>
    public static implicit operator ValueSupplier<T>(T value) => new(value);

    /// <inheritdoc/>
    public override string? ToString() => value?.ToString();
}

/// <summary>
/// Represents implementation of <see cref="ISupplier{TResult}"/> that delegates
/// invocation to the delegate of type <see cref="Func{TResult}"/>.
/// </summary>
/// <typeparam name="TResult">The type of the result.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly record struct DelegatingSupplier<TResult> : ISupplier<TResult>
    where TResult : allows ref struct
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

    /// <inheritdoc/>
    void IFunctional.DynamicInvoke(ref readonly Variant args, int count, scoped Variant result)
        => ISupplier<TResult>.PrepareInvocation(count, result) = func();

    /// <inheritdoc />
    public override string? ToString() => func?.ToString();

    /// <summary>
    /// Wraps the delegate instance.
    /// </summary>
    /// <param name="func">The delegate instance.</param>
    /// <returns>The supplier represented by the delegate.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
    public static implicit operator DelegatingSupplier<TResult>(Func<TResult> func) => new(func);
}