using System.Runtime.InteropServices;

namespace DotNext;

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
public interface IConsumer<in T> : IFunctional<Action<T>>
{
    /// <summary>
    /// Invokes the consumer.
    /// </summary>
    /// <param name="arg">The first argument.</param>
    void Invoke(T arg);

    /// <inheritdoc />
    Action<T> IFunctional<Action<T>>.ToDelegate() => Invoke;
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
{
    private readonly delegate*<T, void> ptr = ptr is not null ? ptr : throw new ArgumentNullException(nameof(ptr));

    /// <summary>
    /// Gets a value indicating that this function pointer is zero.
    /// </summary>
    public bool IsEmpty => ptr is null;

    /// <inheritdoc />
    void IConsumer<T>.Invoke(T arg) => ptr(arg);

    /// <summary>
    /// Converts this consumer to the delegate of type <see cref="Action{T}"/>.
    /// </summary>
    /// <returns>The delegate representing the wrapped method.</returns>
    public Action<T> ToDelegate() => DelegateHelpers.CreateDelegate<T>(ptr);

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
    public static explicit operator Action<T>(Consumer<T> consumer) => consumer.ToDelegate();
}

/// <summary>
/// Represents implementation of <see cref="IConsumer{T}"/> interface
/// with the support of closure that is not allocated on the heap.
/// </summary>
/// <typeparam name="TContext">The type describing closure.</typeparam>
/// <typeparam name="T">The type of the consumer argument.</typeparam>
/// <remarks>
/// Wraps the function pointer.
/// </remarks>
/// <param name="ptr">The function pointer.</param>
/// <param name="context">The context to be passed to the function pointer.</param>
/// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
[CLSCompliant(false)]
[StructLayout(LayoutKind.Auto)]
public readonly unsafe struct ConsumerClosure<TContext, T>(delegate*<in TContext, T, void> ptr, TContext context) : IConsumer<T>
{
    private readonly delegate*<in TContext, T, void> ptr = ptr is not null ? ptr : throw new ArgumentNullException(nameof(ptr));
    private readonly TContext context = context;

    /// <summary>
    /// Gets a value indicating that this function pointer is zero.
    /// </summary>
    public bool IsEmpty => ptr is null;

    /// <inheritdoc />
    void IConsumer<T>.Invoke(T arg) => ptr(in context, arg);
}

/// <summary>
/// Represents implementation of <see cref="IConsumer{T}"/> that delegates
/// invocation to the delegate of type <see cref="Action{T}"/>.
/// </summary>
/// <typeparam name="T">The type of the consumer argument.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly record struct DelegatingConsumer<T> : IConsumer<T>, IEquatable<DelegatingConsumer<T>>
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

    /// <inheritdoc />
    Action<T> IFunctional<Action<T>>.ToDelegate() => action;

    /// <inheritdoc />
    public override string? ToString() => action?.ToString();

    /// <summary>
    /// Wraps the delegate.
    /// </summary>
    /// <param name="action">The delegate to be wrapped.</param>
    /// <returns>The consumer representing the delegate.</returns>
    public static implicit operator DelegatingConsumer<T>(Action<T> action) => new(action);
}