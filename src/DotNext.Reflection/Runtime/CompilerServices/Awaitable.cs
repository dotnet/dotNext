using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.CompilerServices;

using Reflection;

/// <summary>
/// Represents awaitable concept type.
/// </summary>
/// <typeparam name="T">The constrained type.</typeparam>
/// <typeparam name="TAwaiter">The type constrained with concept <see cref="Awaiter{TAwaiter}"/>.</typeparam>
/// <seealso href="https://docs.microsoft.com/en-us/dotnet/standard/asynchronous-programming-patterns/task-based-asynchronous-pattern-tap">TAP</seealso>
[Concept]
[StructLayout(LayoutKind.Auto)]
public readonly struct Awaitable<T, [Constraint(typeof(Awaiter<>))] TAwaiter>
    where TAwaiter : ICriticalNotifyCompletion
{
    private static readonly Operator<T, TAwaiter> GetAwaiterMethod = Type<T>.Method.Require<Operator<T, TAwaiter>>(nameof(Task.GetAwaiter), MethodLookup.Instance);

    static Awaitable() => Concept.Assert<Awaiter<TAwaiter>>();

    private readonly T awaitable;

    /// <summary>
    /// Wraps value of type <typeparamref name="T"/> into awaitable value compatible with <see langword="await"/> expression.
    /// </summary>
    /// <param name="awaitable">Underlying awaitable object.</param>
    public Awaitable(T awaitable) => this.awaitable = awaitable;

    /// <summary>
    /// Gets awaiter used to await asynchronous result represented by type <typeparamref name="T"/>.
    /// </summary>
    /// <returns>An awaiter instance.</returns>
    /// <exception cref="InvalidOperationException"><typeparamref name="TAwaiter"/> returned by <typeparamref name="T"/> is <see langword="null"/>.</exception>
    public Awaiter<TAwaiter> GetAwaiter() => new(GetAwaiter(in awaitable));

    /// <summary>
    /// Gets underlying awaitable object.
    /// </summary>
    /// <param name="awaitable">Awaitable object container.</param>
    public static implicit operator T(in Awaitable<T, TAwaiter> awaitable) => awaitable.awaitable;

    /// <summary>
    /// Gets awaiter used to await asynchronous result represented by type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="obj">The object representing asynchronous computing.</param>
    /// <returns>An awaiter instance.</returns>
    /// <exception cref="InvalidOperationException"><typeparamref name="TAwaiter"/> returned by <typeparamref name="T"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNull]
    public static TAwaiter GetAwaiter(in T obj) => GetAwaiterMethod(in obj) ?? throw new InvalidOperationException(ExceptionMessages.AwaiterMustNotBeNull);
}

/// <summary>
/// Represents awaitable concept type for the task type with non-void result.
/// </summary>
/// <typeparam name="T">The constrained type.</typeparam>
/// <typeparam name="TAwaiter">The type constrained with concept <see cref="Awaiter{TAwaiter}"/>.</typeparam>
/// <typeparam name="TResult">The type of asynchronous result.</typeparam>
/// <seealso href="https://docs.microsoft.com/en-us/dotnet/standard/asynchronous-programming-patterns/task-based-asynchronous-pattern-tap">TAP</seealso>
[Concept]
[StructLayout(LayoutKind.Auto)]
public readonly struct Awaitable<T, [Constraint(typeof(Awaiter<,>))] TAwaiter, TResult>
    where TAwaiter : ICriticalNotifyCompletion
{
    private static readonly Operator<T, TAwaiter> GetAwaiterMethod = Type<T>.Method.Require<Operator<T, TAwaiter>>(nameof(Task.GetAwaiter), MethodLookup.Instance);

    static Awaitable() => Concept.Assert<Awaiter<TAwaiter, TResult>>();

    private readonly T awaitable;

    /// <summary>
    /// Wraps value of type <typeparamref name="T"/> into awaitable value compatible with <see langword="await"/> expression.
    /// </summary>
    /// <param name="awaitable">Underlying awaitable object.</param>
    public Awaitable(T awaitable) => this.awaitable = awaitable;

    /// <summary>
    /// Gets awaiter used to await asynchronous result represented by type <typeparamref name="T"/>.
    /// </summary>
    /// <returns>An awaiter instance.</returns>
    /// <exception cref="InvalidOperationException"><typeparamref name="TAwaiter"/> returned by <typeparamref name="T"/> is <see langword="null"/>.</exception>
    public Awaiter<TAwaiter, TResult> GetAwaiter() => new(GetAwaiter(in awaitable));

    /// <summary>
    /// Gets underlying awaitable object.
    /// </summary>
    /// <param name="awaitable">Awaitable object container.</param>
    public static implicit operator T(in Awaitable<T, TAwaiter, TResult> awaitable) => awaitable.awaitable;

    /// <summary>
    /// Gets awaiter used to await asynchronous result represented by type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="obj">The object representing asynchronous computing.</param>
    /// <returns>An awaiter instance.</returns>
    /// <exception cref="InvalidOperationException"><typeparamref name="TAwaiter"/> returned by <typeparamref name="T"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNull]
    public static TAwaiter GetAwaiter(in T obj) => GetAwaiterMethod(in obj) ?? throw new InvalidOperationException(ExceptionMessages.AwaiterMustNotBeNull);
}