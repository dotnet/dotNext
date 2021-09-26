using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using RuntimeHelpers = System.Runtime.CompilerServices.RuntimeHelpers;

namespace DotNext;

using FunctionalInterfaceAttribute = Runtime.CompilerServices.FunctionalInterfaceAttribute;

/// <summary>
/// Represents functional interface returning no value and
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
[FunctionalInterface]
public interface IConsumer<in T>
{
    /// <summary>
    /// Invokes the consumer.
    /// </summary>
    /// <param name="arg">The first argument.</param>
    void Invoke(T arg);
}

/// <summary>
/// Represents typed function pointer implementing <see cref="IConsumer{T}"/>.
/// </summary>
/// <typeparam name="T">The type of the consumer argument.</typeparam>
[CLSCompliant(false)]
[StructLayout(LayoutKind.Auto)]
public readonly unsafe struct Consumer<T> : IConsumer<T>
{
    private readonly delegate*<T, void> ptr;

    /// <summary>
    /// Wraps the function pointer.
    /// </summary>
    /// <param name="ptr">The function pointer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    public Consumer(delegate*<T, void> ptr)
        => this.ptr = ptr == null ? throw new ArgumentNullException(nameof(ptr)) : ptr;

    /// <summary>
    /// Gets a value indicating that this function pointer is zero.
    /// </summary>
    public bool IsEmpty => ptr == null;

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
[CLSCompliant(false)]
[StructLayout(LayoutKind.Auto)]
public readonly unsafe struct ConsumerClosure<TContext, T> : IConsumer<T>
{
    private readonly delegate*<in TContext, T, void> ptr;
    private readonly TContext context;

    /// <summary>
    /// Wraps the function pointer.
    /// </summary>
    /// <param name="ptr">The function pointer.</param>
    /// <param name="context">The context to be passed to the function pointer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    public ConsumerClosure(delegate*<in TContext, T, void> ptr, TContext context)
    {
        this.ptr = ptr == null ? throw new ArgumentNullException(nameof(ptr)) : ptr;
        this.context = context;
    }

    /// <summary>
    /// Gets a value indicating that this function pointer is zero.
    /// </summary>
    public bool IsEmpty => ptr == null;

    /// <inheritdoc />
    void IConsumer<T>.Invoke(T arg) => ptr(in context, arg);
}

/// <summary>
/// Represents implementation of <see cref="IConsumer{T}"/> that delegates
/// invocation to the delegate of type <see cref="Action{T}"/>.
/// </summary>
/// <typeparam name="T">The type of the consumer argument.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly struct DelegatingConsumer<T> : IConsumer<T>
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

    /// <summary>
    /// Determines whether this object contains the same delegate instance as the specified object.
    /// </summary>
    /// <param name="other">The object to compare.</param>
    /// <returns><see langword="true"/> if this object contains the same delegate instance as <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
    public bool Equals(DelegatingConsumer<T> other)
        => ReferenceEquals(action, other.action);

    /// <summary>
    /// Determines whether this object contains the same delegate instance as the specified object.
    /// </summary>
    /// <param name="other">The object to compare.</param>
    /// <returns><see langword="true"/> if this object contains the same delegate instance as <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
    public override bool Equals([NotNullWhen(true)] object? other) => other is DelegatingConsumer<T> consumer && Equals(consumer);

    /// <summary>
    /// Gets the hash code representing identity of the stored delegate instance.
    /// </summary>
    /// <returns>The hash code representing identity of the stored delegate instance.</returns>
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(action);

    /// <summary>
    /// Determines whether the two objects contain references to the same delegate instance.
    /// </summary>
    /// <param name="x">The first object to compare.</param>
    /// <param name="y">The second object to compare.</param>
    /// <returns><see langword="true"/> if the both objects contain references the same delegate instance; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(DelegatingConsumer<T> x, DelegatingConsumer<T> y)
        => x.Equals(y);

    /// <summary>
    /// Determines whether the two objects contain references to the different delegate instances.
    /// </summary>
    /// <param name="x">The first object to compare.</param>
    /// <param name="y">The second object to compare.</param>
    /// <returns><see langword="true"/> if the both objects contain references the different delegate instances; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(DelegatingConsumer<T> x, DelegatingConsumer<T> y)
        => !x.Equals(y);
}