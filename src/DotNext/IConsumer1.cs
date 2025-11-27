using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext;

using Runtime;
using Runtime.CompilerServices;

/// <summary>
/// Represents functional interface returning no value and
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
public interface IConsumer<in T> : IFunctional
    where T : allows ref struct
{
    /// <summary>
    /// Invokes the consumer.
    /// </summary>
    /// <param name="arg">The first argument.</param>
    void Invoke(T arg);

    /// <inheritdoc/>
    void IFunctional.DynamicInvoke(scoped ref Variant args, int count, scoped Variant result)
    {
        PrepareInvocation(count);
        Invoke(args.ReadOnly<T>());
    }

    internal static void PrepareInvocation(int count,
        [CallerArgumentExpression(nameof(count))]
        string? countArgName = null)
        => ArgumentOutOfRangeException.ThrowIfNotEqual(count, 1, countArgName);
}

/// <summary>
/// Represents typed function pointer implementing <see cref="IConsumer{T}"/>.
/// </summary>
/// <typeparam name="T">The type of the consumer argument.</typeparam>
/// <remarks>
/// Wraps the function pointer.
/// </remarks>
/// <param name="ptr">The function pointer.</param>
/// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
[CLSCompliant(false)]
[StructLayout(LayoutKind.Auto)]
public readonly unsafe struct Consumer<T>(delegate*<T, void> ptr) : IConsumer<T>
    where T : allows ref struct
{
    private readonly delegate*<T, void> ptr = ptr is not null ? ptr : throw new ArgumentNullException(nameof(ptr));

    /// <summary>
    /// Gets a value indicating that this function pointer is zero.
    /// </summary>
    public bool IsEmpty => ptr is null;

    /// <inheritdoc />
    void IConsumer<T>.Invoke(T arg) => ptr(arg);

    /// <inheritdoc/>
    void IFunctional.DynamicInvoke(scoped ref Variant args, int count, scoped Variant result)
    {
        IConsumer<T>.PrepareInvocation(count);
        ptr(args.ReadOnly<T>());
    }

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
    public static implicit operator Consumer<T>(delegate*<T, void> ptr) => new(ptr);

    /// <summary>
    /// Converts this consumer to the delegate of type <see cref="Action{T}"/>.
    /// </summary>
    /// <param name="consumer">The value representing the pointer to the method.</param>
    /// <returns>The delegate representing the wrapped method.</returns>
    public static explicit operator Action<T>(Consumer<T> consumer)
        => Action<T>.FromPointer(consumer.ptr);
}

/// <summary>
/// Represents implementation of <see cref="IConsumer{T}"/> that delegates
/// invocation to the delegate of type <see cref="Action{T}"/>.
/// </summary>
/// <typeparam name="T">The type of the consumer argument.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly record struct DelegatingConsumer<T> : IConsumer<T>
    where T : allows ref struct
{
    private readonly Action<T> action;

    /// <summary>
    /// Wraps the delegate instance.
    /// </summary>
    /// <param name="action">The delegate instance.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public DelegatingConsumer(Action<T> action)
        => this.action = action ?? throw new ArgumentNullException(nameof(action));

    /// <summary>
    /// Gets a value indicating that the underlying delegate is <see langword="null"/>.
    /// </summary>
    public bool IsEmpty => action is null;

    /// <inheritdoc />
    void IConsumer<T>.Invoke(T arg) => action(arg);

    /// <inheritdoc/>
    void IFunctional.DynamicInvoke(scoped ref Variant args, int count, scoped Variant result)
    {
        IConsumer<T>.PrepareInvocation(count);
        action(args.ReadOnly<T>());
    }

    /// <inheritdoc />
    public override string? ToString() => action?.ToString();

    /// <summary>
    /// Wraps the delegate.
    /// </summary>
    /// <param name="action">The delegate to be wrapped.</param>
    /// <returns>The consumer representing the delegate.</returns>
    public static implicit operator DelegatingConsumer<T>(Action<T> action) => new(action);
}