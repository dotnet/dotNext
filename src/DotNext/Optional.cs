using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace DotNext;

using System.Threading.Tasks;
using Runtime.CompilerServices;
using Intrinsics = Runtime.Intrinsics;

/// <summary>
/// Various extension and factory methods for constructing optional value.
/// </summary>
public static class Optional
{
    /// <summary>
    /// If a value is present, returns the value, otherwise <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="task">The task returning optional value.</param>
    /// <returns>Nullable value.</returns>
    public static async Task<T?> OrNull<T>(this Task<Optional<T>> task)
        where T : struct
        => (await task.ConfigureAwait(false)).OrNull();

    /// <summary>
    /// Returns the value if present; otherwise return default value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="task">The task returning optional value.</param>
    /// <param name="defaultValue">The value to be returned if there is no value present.</param>
    /// <returns>The value, if present, otherwise default.</returns>
    public static async Task<T?> Or<T>(this Task<Optional<T>> task, T? defaultValue)
        => (await task.ConfigureAwait(false)).Or(defaultValue);

    /// <summary>
    /// Returns a task that contains unwrapped value; or exception if <see cref="Optional{T}"/> has no value.
    /// </summary>
    /// <param name="task">The task representing optional value.</param>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <returns>The task containing a value of type <typeparamref name="T"/>; or the exception if <see cref="Optional{T}"/> has no value.</returns>
    /// <exception cref="InvalidOperationException">No value is present.</exception>
    public static async Task<T> Flatten<T>(this Task<Optional<T>> task)
        => (await task.ConfigureAwait(false)).Value;

    /// <summary>
    /// If a value is present, apply the provided mapping function to it, and if the result is
    /// non-null, return an Optional describing the result. Otherwise, returns <see cref="Optional{T}.None"/>.
    /// </summary>
    /// <typeparam name="TInput">The type of stored in the Optional container.</typeparam>
    /// <typeparam name="TOutput">The type of the mapping function result.</typeparam>
    /// <param name="task">The task containing Optional value.</param>
    /// <param name="converter">A mapping function to be applied to the value, if present.</param>
    /// <returns>An Optional describing the result of applying a mapping function to the value of this Optional, if a value is present, otherwise <see cref="Optional{T}.None"/>.</returns>
    public static async Task<Optional<TOutput>> Convert<TInput, TOutput>(this Task<Optional<TInput>> task, Converter<TInput, TOutput> converter)
        => (await task.ConfigureAwait(false)).Convert(converter);

    /// <summary>
    /// If a value is present, apply the provided mapping function to it, and if the result is
    /// non-null, return an Optional describing the result. Otherwise returns <see cref="Optional{T}.None"/>.
    /// </summary>
    /// <typeparam name="TInput">The type of stored in the Optional container.</typeparam>
    /// <typeparam name="TOutput">The type of the result of the mapping function.</typeparam>
    /// <param name="task">The task containing Optional value.</param>
    /// <param name="converter">A mapping function to be applied to the value, if present.</param>
    /// <returns>An Optional describing the result of applying a mapping function to the value of this Optional, if a value is present, otherwise <see cref="Optional{T}.None"/>.</returns>
    public static async Task<Optional<TOutput>> Convert<TInput, TOutput>(this Task<Optional<TInput>> task, Converter<TInput, Optional<TOutput>> converter)
        => (await task.ConfigureAwait(false)).Convert(converter);

    /// <summary>
    /// If a value is present, apply the provided mapping function to it, and if the result is
    /// non-null, return an Optional describing the result. Otherwise, returns <see cref="Optional{T}.None"/>.
    /// </summary>
    /// <typeparam name="TInput">The type of stored in the Optional container.</typeparam>
    /// <typeparam name="TOutput">The type of the mapping function result.</typeparam>
    /// <param name="task">The task containing Optional value.</param>
    /// <param name="converter">A mapping function to be applied to the value, if present.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>An Optional describing the result of applying a mapping function to the value of this Optional, if a value is present, otherwise <see cref="Optional{T}.None"/>.</returns>
    public static async Task<Optional<TOutput>> Convert<TInput, TOutput>(this Task<Optional<TInput>> task,
        Func<TInput, CancellationToken, Task<TOutput>> converter, CancellationToken token = default)
    {
        var optional = await task.ConfigureAwait(false);
        return optional.HasValue
            ? await converter.Invoke(optional.ValueOrDefault, token).ConfigureAwait(false)
            : optional.IsNull && Intrinsics.IsNullable<TOutput>()
                ? new(default)
                : Optional<TOutput>.None;
    }

    /// <summary>
    /// If a value is present, apply the provided mapping function to it, and if the result is
    /// non-null, return an Optional describing the result. Otherwise returns <see cref="Optional{T}.None"/>.
    /// </summary>
    /// <typeparam name="TInput">The type of stored in the Optional container.</typeparam>
    /// <typeparam name="TOutput">The type of the result of the mapping function.</typeparam>
    /// <param name="task">The task containing Optional value.</param>
    /// <param name="converter">A mapping function to be applied to the value, if present.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>An Optional describing the result of applying a mapping function to the value of this Optional, if a value is present, otherwise <see cref="Optional{T}.None"/>.</returns>
    public static async Task<Optional<TOutput>> Convert<TInput, TOutput>(this Task<Optional<TInput>> task,
        Func<TInput, CancellationToken, Task<Optional<TOutput>>> converter, CancellationToken token = default)
    {
        var optional = await task.ConfigureAwait(false);
        return optional.HasValue
            ? await converter.Invoke(optional.ValueOrDefault, token).ConfigureAwait(false)
            : optional.IsNull && Intrinsics.IsNullable<TOutput>()
                ? new(default)
                : Optional<TOutput>.None;
    }

    /// <summary>
    /// Creates <see cref="Result{T}"/> from <see cref="Optional{T}"/> instance.
    /// </summary>
    /// <param name="optional">The optional value.</param>
    /// <returns>The converted optional value.</returns>
    public static Result<T> ToResult<T>(this in Optional<T> optional)
        => Result<T>.FromOptional(optional);

    /// <summary>
    /// Creates <see cref="Result{T}"/> from <see cref="Optional{T}"/> instance.
    /// </summary>
    /// <param name="task">The task containing Optional value.</param>
    /// <returns>The converted optional value.</returns>
    public static async Task<Result<T>> ToResult<T>(this Task<Optional<T>> task)
        => Result<T>.FromOptional(await task.ConfigureAwait(false));

    /// <summary>
    /// Creates <see cref="Result{T, TError}"/> from <see cref="Optional{T}"/> instance.
    /// </summary>
    /// <param name="optional">The optional value.</param>
    /// <param name="error">The error code to apply if the value is not present.</param>
    /// <returns>The converted optional value.</returns>
    public static Result<T, TError> ToResult<T, TError>(this in Optional<T> optional, TError error)
        where TError : struct, Enum
        => optional.HasValue ? new(optional.Value) : new(error);

    /// <summary>
    /// Creates <see cref="Result{T, TError}"/> from <see cref="Optional{T}"/> instance.
    /// </summary>
    /// <param name="task">The task containing Optional value.</param>
    /// <param name="error">The error code to apply if the value is not present.</param>
    /// <returns>The converted optional value.</returns>
    public static async Task<Result<T, TError>> ToResult<T, TError>(this Task<Optional<T>> task, TError error)
        where TError : struct, Enum
    {
        var optional = await task.ConfigureAwait(false);
        return optional.HasValue ? new(optional.Value) : new(error);
    }

    /// <summary>
    /// If a value is present, returns the value, otherwise throw exception.
    /// </summary>
    /// <param name="task">The task returning optional value.</param>
    /// <typeparam name="T">Type of the value.</typeparam>
    /// <typeparam name="TException">Type of exception to throw.</typeparam>
    /// <returns>The value, if present.</returns>
    public static async Task<T> OrThrow<T, TException>(this Task<Optional<T>> task)
        where TException : Exception, new()
        => (await task.ConfigureAwait(false)).OrThrow<TException>();

    /// <summary>
    /// If a value is present, returns the value, otherwise throw exception.
    /// </summary>
    /// <typeparam name="T">Type of the value.</typeparam>
    /// <typeparam name="TException">Type of exception to throw.</typeparam>
    /// <param name="task">The task returning optional value.</param>
    /// <param name="exceptionFactory">Exception factory.</param>
    /// <returns>The value, if present.</returns>
    public static async Task<T> OrThrow<T, TException>(this Task<Optional<T>> task, Func<TException> exceptionFactory)
        where TException : Exception
        => (await task.ConfigureAwait(false)).OrThrow(exceptionFactory);

    /// <summary>
    /// Returns the value if present; otherwise invoke delegate.
    /// </summary>
    /// <typeparam name="T">Type of the value.</typeparam>
    /// <param name="task">The task returning optional value.</param>
    /// <param name="defaultFunc">A delegate to be invoked if value is not present.</param>
    /// <returns>The value, if present, otherwise returned from delegate.</returns>
    public static async Task<T> OrInvoke<T>(this Task<Optional<T>> task, Func<T> defaultFunc)
        => (await task.ConfigureAwait(false)).OrInvoke(defaultFunc);

    /// <summary>
    /// If a value is present, returns the value, otherwise return default value.
    /// </summary>
    /// <typeparam name="T">Type of the value.</typeparam>
    /// <param name="task">The task returning optional value.</param>
    /// <returns>The value, if present, otherwise default.</returns>
    public static async Task<T?> OrDefault<T>(this Task<Optional<T>> task)
        => (await task.ConfigureAwait(false)).ValueOrDefault;

    /// <summary>
    /// If a value is present, and the value matches the given predicate,
    /// return an Optional describing the value, otherwise return an empty Optional.
    /// </summary>
    /// <typeparam name="T">Type of the value.</typeparam>
    /// <param name="task">The task returning optional value.</param>
    /// <param name="condition">A predicate to apply to the value, if present.</param>
    /// <returns>An Optional describing the value of this Optional if a value is present and the value matches the given predicate, otherwise an empty Optional.</returns>
    public static async Task<Optional<T>> If<T>(this Task<Optional<T>> task, Predicate<T> condition)
        => (await task.ConfigureAwait(false)).If(condition);

    /// <summary>
    /// Indicates that specified type is optional type.
    /// </summary>
    /// <param name="optionalType">The type to check.</param>
    /// <returns><see langword="true"/>, if specified type is optional type; otherwise, <see langword="false"/>.</returns>
    public static bool IsOptional(this Type optionalType) => optionalType.IsConstructedGenericType && optionalType.GetGenericTypeDefinition() == typeof(Optional<>);

    /// <summary>
    /// Returns the underlying type argument of the specified optional type.
    /// </summary>
    /// <param name="optionalType">Optional type.</param>
    /// <returns>Underlying type argument of optional type; otherwise, <see langword="null"/>.</returns>
    public static Type? GetUnderlyingType(Type optionalType) => IsOptional(optionalType) ? optionalType.GetGenericArguments()[0] : null;

    /// <summary>
    /// Constructs optional value from nullable value type.
    /// </summary>
    /// <typeparam name="T">Type of value.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <returns>The value wrapped into Optional container.</returns>
    public static Optional<T> ToOptional<T>(this in T? value)
        where T : struct
        => value.HasValue ? Some(value.GetValueOrDefault()) : None<T>();

    /// <summary>
    /// If a value is present, returns the value, otherwise <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <param name="value">Optional value.</param>
    /// <returns>Nullable value.</returns>
    public static T? OrNull<T>(this in Optional<T> value)
        where T : struct
        => value.HasValue ? value.ValueOrDefault : null;

    /// <summary>
    /// Returns the second value if the first is empty.
    /// </summary>
    /// <param name="first">The first optional value.</param>
    /// <param name="second">The second optional value.</param>
    /// <typeparam name="T">Type of value.</typeparam>
    /// <returns>The second value if the first is empty; otherwise, the first value.</returns>
    public static ref readonly Optional<T> Coalesce<T>(this in Optional<T> first, in Optional<T> second) => ref first.HasValue ? ref first : ref second;

    /// <summary>
    /// Returns empty value.
    /// </summary>
    /// <typeparam name="T">The type of empty result.</typeparam>
    /// <returns>The empty value.</returns>
    public static Optional<T> None<T>() => Optional<T>.None;

    /// <summary>
    /// Wraps the value to <see cref="Optional{T}"/> container.
    /// </summary>
    /// <param name="value">The value to be wrapped.</param>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <returns>The optional container.</returns>
    public static Optional<T> Some<T>([DisallowNull] T value) => new(value);

    /// <summary>
    /// Wraps <see langword="null"/> value to <see cref="Optional{T}"/> container.
    /// </summary>
    /// <typeparam name="T">The reference type.</typeparam>
    /// <returns>The <see cref="Optional{T}"/> instance representing <see langword="null"/> value.</returns>
    public static Optional<T> Null<T>()
        where T : class?
        => new(null);

    /// <summary>
    /// Flattens the nested optional value.
    /// </summary>
    /// <typeparam name="T">The type of the underlying value.</typeparam>
    /// <param name="optional">The nested optional value.</param>
    /// <returns>Flattened value.</returns>
    public static Optional<T> Flatten<T>(this in Optional<Optional<T>> optional)
        => new(in optional);
}

/// <summary>
/// A container object which may or may not contain a value.
/// </summary>
/// <typeparam name="T">Type of value.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly struct Optional<T> : IEquatable<Optional<T>>, IEquatable<T>, IStructuralEquatable, IOptionMonad<T, Optional<T>>
{
    private const byte UndefinedValue = 0;
    private const byte NullValue = 1;
    private const byte NotEmptyValue = 3;

    private static readonly bool IsOptional;

    static Optional()
    {
        var type = typeof(T);
        IsOptional = type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(Optional<>);
    }

    private readonly T? value;
    private readonly byte kind;

    /// <summary>
    /// Constructs non-empty container.
    /// </summary>
    /// <param name="value">A value to be placed into container.</param>
    /// <remarks>
    /// The property <see langword="IsNull"/> of the constructed object may be <see langword="true"/>
    /// if <paramref name="value"/> is <see langword="null"/>.
    /// The property <see langword="IsUndefined"/> of the constructed object is always <see langword="false"/>.
    /// </remarks>
    public Optional(T? value)
    {
        this.value = value;
        kind = value is null ? NullValue : IsOptional ? GetKindUnsafe(ref value) : NotEmptyValue;
    }

    internal Optional(in Optional<Optional<T>> value)
    {
        this.value = value.value.value;
        kind = value.kind;
    }

    private static byte GetKindUnsafe([DisallowNull] ref T optionalValue)
    {
        Debug.Assert(IsOptional);

        return optionalValue.Equals(null)
            ? NullValue
            : optionalValue.Equals(Sentinel.Instance)
            ? UndefinedValue
            : NotEmptyValue;
    }

    /// <summary>
    /// Converts the monad to <see cref="Optional{T}"/>.
    /// </summary>
    /// <typeparam name="TMonad">The type of the monad.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <returns>The value represented as <see cref="Optional{T}"/> or <see cref="Optional{T}.None"/> if there is no value.</returns>
    public static Optional<T> Create<TMonad>(TMonad value)
        where TMonad : struct, IOptionMonad<T>
        => value is Optional<T>
            ? Unsafe.BitCast<TMonad, Optional<T>>(value)
            : value.TryGet(out var result)
                ? new(result)
                : None;

    /// <summary>
    /// Determines whether the object represents meaningful value.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="value"/> is not null,
    /// or <see cref="Nullable{T}.HasValue"/> property is <see langword="true"/>,
    /// or <see cref="Optional{T}.HasValue"/> property is <see langword="true"/>;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static bool IsValueDefined([NotNullWhen(true)] T? value)
        => value is not null && (!IsOptional || GetKindUnsafe(ref value) is NotEmptyValue);

    /// <summary>
    /// Represents optional container without value.
    /// </summary>
    /// <remarks>
    /// The property <see cref="IsUndefined"/> of returned object is always <see langword="true"/>.
    /// </remarks>
    public static Optional<T> None => default;

    /// <summary>
    /// Indicates whether the value is present.
    /// </summary>
    /// <remarks>
    /// If this property is <see langword="true"/> then <see cref="IsUndefined"/> and <see cref="IsNull"/>
    /// equal to <see langword="false"/>.
    /// </remarks>
    [MemberNotNullWhen(true, nameof(value))]
    [MemberNotNullWhen(true, nameof(ValueOrDefault))]
    public bool HasValue => kind is NotEmptyValue;

    /// <summary>
    /// Indicates that the value is undefined.
    /// </summary>
    /// <seealso cref="None"/>
    public bool IsUndefined => kind is UndefinedValue;

    /// <summary>
    /// Indicates that the value is <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// This property returns <see langword="true"/> only if this instance
    /// was constructed using <see cref="Optional{T}(T)"/> with <see langword="null"/> argument.
    /// </remarks>
    public bool IsNull => kind is NullValue;

    /// <summary>
    /// Boxes value encapsulated by this object.
    /// </summary>
    /// <returns>The boxed value.</returns>
    public Optional<object> Box() => IsUndefined ? default : new(value);

    /// <summary>
    /// Attempts to extract value from container if it is present.
    /// </summary>
    /// <param name="value">Extracted value.</param>
    /// <returns><see langword="true"/> if value is present; otherwise, <see langword="false"/>.</returns>
    public bool TryGet([MaybeNullWhen(false)] out T value)
    {
        value = this.value;
        return HasValue;
    }

    /// <summary>
    /// Attempts to extract value from container if it is present.
    /// </summary>
    /// <param name="value">Extracted value.</param>
    /// <param name="isNull"><see langword="true"/> if underlying value is <see langword="null"/>; otherwise, <see langword="false"/>.</param>
    /// <returns><see langword="true"/> if value is present; otherwise, <see langword="false"/>.</returns>
    public bool TryGet([MaybeNullWhen(false)] out T value, out bool isNull)
    {
        value = this.value!;
        switch (kind)
        {
            default:
                isNull = false;
                return false;
            case NullValue:
                isNull = true;
                return false;
            case NotEmptyValue:
                Debug.Assert(value is not null);
                isNull = false;
                return true;
        }
    }

    /// <summary>
    /// Returns the value if present; otherwise return default value.
    /// </summary>
    /// <param name="defaultValue">The value to be returned if there is no value present.</param>
    /// <returns>The value, if present, otherwise <paramref name="defaultValue"/>.</returns>
    [return: NotNullIfNotNull(nameof(defaultValue))]
    public T? Or(T? defaultValue) => HasValue ? value : defaultValue;

    /// <summary>
    /// Concatenates optional values.
    /// </summary>
    /// <param name="other"></param>
    /// <typeparam name="TOther"></typeparam>
    /// <returns>The optional value that is defined only when both containers have values.</returns>
    public Optional<(T, TOther)> Concat<TOther>(in Optional<TOther> other)
        => HasValue && other.HasValue ? new((ValueOrDefault, other.ValueOrDefault)) : default;

    /// <summary>
    /// If a value is present, returns the value, otherwise throw exception.
    /// </summary>
    /// <typeparam name="TException">Type of exception to throw.</typeparam>
    /// <returns>The value, if present.</returns>
    [return: NotNull]
    [MemberNotNull(nameof(ValueOrDefault))]
    public T OrThrow<TException>()
        where TException : Exception, new()
        => OrThrow(new Activator<TException>());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [MemberNotNull(nameof(ValueOrDefault))]
    [return: NotNull]
    private T OrThrow<TFactory>(TFactory exceptionFactory)
        where TFactory : struct, ISupplier<Exception>
    {
        Validate(exceptionFactory);
        return value;
    }

    [MemberNotNull(nameof(value))]
    [MemberNotNull(nameof(ValueOrDefault))]
    private void Validate<TFactory>(TFactory exceptionFactory)
        where TFactory : struct, ISupplier<Exception>
    {
        if (!HasValue)
            Throw(exceptionFactory);

        [DoesNotReturn]
        [StackTraceHidden]
        static void Throw(TFactory exceptionFactory) => throw exceptionFactory.Invoke();
    }

    /// <summary>
    /// If a value is present, returns the value, otherwise throw exception.
    /// </summary>
    /// <param name="exceptionFactory">Exception factory.</param>
    /// <returns>The value, if present.</returns>
    [MemberNotNull(nameof(ValueOrDefault))]
    [return: NotNull]
    public T OrThrow(Func<Exception> exceptionFactory)
        => OrThrow<DelegatingSupplier<Exception>>(exceptionFactory);

    /// <summary>
    /// If a value is present, returns the value, otherwise throw exception.
    /// </summary>
    /// <param name="exceptionFactory">Exception factory.</param>
    /// <returns>The value, if present.</returns>
    [CLSCompliant(false)]
    [MemberNotNull(nameof(ValueOrDefault))]
    [return: NotNull]
    public unsafe T OrThrow(delegate*<Exception> exceptionFactory)
        => OrThrow<Supplier<Exception>>(exceptionFactory);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private T OrInvoke<TSupplier>(TSupplier defaultFunc)
        where TSupplier : struct, ISupplier<T>
        => HasValue ? value : defaultFunc.Invoke();

    /// <summary>
    /// Returns the value if present; otherwise invoke delegate.
    /// </summary>
    /// <param name="defaultFunc">A delegate to be invoked if value is not present.</param>
    /// <returns>The value, if present, otherwise returned from delegate.</returns>
    public T OrInvoke(Func<T> defaultFunc) => OrInvoke<DelegatingSupplier<T>>(defaultFunc);

    /// <summary>
    /// Returns the value if present; otherwise invoke delegate.
    /// </summary>
    /// <param name="defaultFunc">A delegate to be invoked if value is not present.</param>
    /// <returns>The value, if present, otherwise returned from delegate.</returns>
    [CLSCompliant(false)]
    public unsafe T OrInvoke(delegate*<T> defaultFunc) => OrInvoke<Supplier<T>>(defaultFunc);

    /// <summary>
    /// If a value is present, returns the value, otherwise default value.
    /// </summary>
    /// <value>The value, if present, otherwise default.</value>
    public T? ValueOrDefault => value;

    /// <summary>
    /// If a value is present, returns the value, otherwise throw exception.
    /// </summary>
    /// <exception cref="InvalidOperationException">No value is present.</exception>
    [DisallowNull]
    public T Value
    {
        get
        {
            Validate();
            return value;
        }
    }

    /// <summary>
    /// Gets an immutable reference to the value in the container.
    /// </summary>
    /// <exception cref="InvalidOperationException">No value is present.</exception>
    [UnscopedRef]
    [DisallowNull]
    [JsonIgnore]
    public ref readonly T ValueRef
    {
        get
        {
            Validate();
            return ref value!;
        }
    }

    [UnscopedRef]
    private ref readonly T GetReference<TException>(TException exceptionFactory)
        where TException : struct, ISupplier<Exception>
    {
        Validate(exceptionFactory);
        return ref value!;
    }

    /// <summary>
    /// Gets an immutable reference to the value in the container.
    /// </summary>
    /// <typeparam name="TException">The type of the exception to throw if the optional container has no value.</typeparam>
    /// <returns>The immutable reference to the value in the container.</returns>
    [UnscopedRef]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public ref readonly T GetReference<TException>()
        where TException : Exception, new()
        => ref GetReference(new Activator<TException>());

    /// <summary>
    /// Gets an immutable reference to the value in the container.
    /// </summary>
    /// <param name="exceptionFactory">The factory used to produce exception if the container has no value.</param>
    /// <returns>The immutable reference to the value in the container.</returns>
    [UnscopedRef]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public ref readonly T GetReference(Func<Exception> exceptionFactory)
        => ref GetReference<DelegatingSupplier<Exception>>(exceptionFactory);

    /// <summary>
    /// Gets an immutable reference to the value in the container.
    /// </summary>
    /// <param name="exceptionFactory">The factory used to produce exception if the container has no value.</param>
    /// <returns>The immutable reference to the value in the container.</returns>
    [CLSCompliant(false)]
    [UnscopedRef]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public unsafe ref readonly T GetReference(delegate*<Exception> exceptionFactory)
        => ref GetReference<Supplier<Exception>>(exceptionFactory);

    [MemberNotNull(nameof(value))]
    private void Validate()
    {
        var kind = this.kind;

        if (kind is NotEmptyValue)
        {
            Debug.Assert(value is not null);
        }
        else
        {
            Throw(kind is UndefinedValue);
        }

        [StackTraceHidden]
        [DoesNotReturn]
        static void Throw(bool isUndefined)
            => throw new InvalidOperationException(isUndefined ? ExceptionMessages.OptionalNoValue : ExceptionMessages.OptionalNullValue);
    }

    /// <inheritdoc />
    object? ISupplier<object?>.Invoke() => HasValue ? value : null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Optional<TResult> Convert<TResult, TConverter>(TConverter converter)
        where TConverter : struct, ISupplier<T, TResult>
        => HasValue ? converter.Invoke(value) : IsNull && Intrinsics.IsNullable<TResult>() ? new(default) : Optional<TResult>.None;

    /// <summary>
    /// If a value is present, apply the provided mapping function to it, and if the result is
    /// non-null, return an Optional describing the result. Otherwise, returns <see cref="None"/>.
    /// </summary>
    /// <typeparam name="TResult">The type of the mapping function result.</typeparam>
    /// <param name="mapper">A mapping function to be applied to the value, if present.</param>
    /// <returns>An Optional describing the result of applying a mapping function to the value of this Optional, if a value is present, otherwise <see cref="None"/>.</returns>
    public Optional<TResult> Convert<TResult>(Converter<T, TResult> mapper)
        => Convert<TResult, DelegatingConverter<T, TResult>>(mapper);

    /// <summary>
    /// If a value is present, apply the provided mapping function to it, and if the result is
    /// non-null, return an Optional describing the result. Otherwise, returns <see cref="None"/>.
    /// </summary>
    /// <typeparam name="TResult">The type of the mapping function result.</typeparam>
    /// <param name="mapper">A mapping function to be applied to the value, if present.</param>
    /// <returns>An Optional describing the result of applying a mapping function to the value of this Optional, if a value is present, otherwise <see cref="None"/>.</returns>
    [CLSCompliant(false)]
    public unsafe Optional<TResult> Convert<TResult>(delegate*<T, TResult> mapper)
        => Convert<TResult, Supplier<T, TResult>>(mapper);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Optional<TResult> ConvertOptional<TResult, TConverter>(TConverter converter)
        where TConverter : struct, ISupplier<T, Optional<TResult>>
        => HasValue ? converter.Invoke(value) : Optional<TResult>.None;

    /// <summary>
    /// If a value is present, apply the provided mapping function to it, and if the result is
    /// non-null, return an Optional describing the result. Otherwise, returns <see cref="None"/>.
    /// </summary>
    /// <typeparam name="TResult">The type of the result of the mapping function.</typeparam>
    /// <param name="mapper">A mapping function to be applied to the value, if present.</param>
    /// <returns>An Optional describing the result of applying a mapping function to the value of this Optional, if a value is present, otherwise <see cref="None"/>.</returns>
    public Optional<TResult> Convert<TResult>(Converter<T, Optional<TResult>> mapper)
        => ConvertOptional<TResult, DelegatingConverter<T, Optional<TResult>>>(mapper);

    /// <summary>
    /// If a value is present, apply the provided mapping function to it, and if the result is
    /// non-null, return an Optional describing the result. Otherwise, returns <see cref="None"/>.
    /// </summary>
    /// <typeparam name="TResult">The type of the result of the mapping function.</typeparam>
    /// <param name="mapper">A mapping function to be applied to the value, if present.</param>
    /// <returns>An Optional describing the result of applying a mapping function to the value of this Optional, if a value is present, otherwise <see cref="None"/>.</returns>
    [CLSCompliant(false)]
    public unsafe Optional<TResult> Convert<TResult>(delegate*<T, Optional<TResult>> mapper)
        => ConvertOptional<TResult, Supplier<T, Optional<TResult>>>(mapper);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task<Optional<TResult>> ConvertTask<TResult>(Func<T, CancellationToken, Task<TResult>> converter, CancellationToken token = default)
        => HasValue ? await converter.Invoke(value, token).ConfigureAwait(false) : Optional<TResult>.None;

    /// <summary>
    /// If a value is present, apply the provided mapping function to it, and if the result is
    /// non-null, return an Optional describing the result. Otherwise, returns <see cref="None"/>.
    /// </summary>
    /// <typeparam name="TResult">The type of the result of the mapping function.</typeparam>
    /// <param name="mapper">A mapping function to be applied to the value, if present.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>An Optional describing the result of applying a mapping function to the value of this Optional, if a value is present, otherwise <see cref="None"/>.</returns>
    public Task<Optional<TResult>> Convert<TResult>(Func<T, CancellationToken, Task<TResult>> mapper, CancellationToken token = default)
        => ConvertTask(mapper, token);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task<Optional<TResult>> ConvertOptionalTask<TResult, TConverter>(TConverter converter)
        where TConverter : struct, ISupplier<T, Task<Optional<TResult>>>
        => HasValue ? converter.Invoke(value) : Task.FromResult(Optional<TResult>.None);

    /// <summary>
    /// If a value is present, apply the provided mapping function to it, and if the result is
    /// non-null, return an Optional describing the result. Otherwise, returns <see cref="None"/>.
    /// </summary>
    /// <typeparam name="TResult">The type of the result of the mapping function.</typeparam>
    /// <param name="mapper">A mapping function to be applied to the value, if present.</param>
    /// <returns>An Optional describing the result of applying a mapping function to the value of this Optional, if a value is present, otherwise <see cref="None"/>.</returns>
    public Task<Optional<TResult>> Convert<TResult>(Converter<T, Task<Optional<TResult>>> mapper)
        => ConvertOptionalTask<TResult, DelegatingConverter<T, Task<Optional<TResult>>>>(mapper);

    /// <summary>
    /// If a value is present, apply the provided mapping function to it, and if the result is
    /// non-null, return an Optional describing the result. Otherwise returns <see cref="None"/>.
    /// </summary>
    /// <typeparam name="TResult">The type of the result of the mapping function.</typeparam>
    /// <param name="mapper">A mapping function to be applied to the value, if present.</param>
    /// <returns>An Optional describing the result of applying a mapping function to the value of this Optional, if a value is present, otherwise <see cref="None"/>.</returns>
    [CLSCompliant(false)]
    public unsafe Task<Optional<TResult>> Convert<TResult>(delegate*<T, Task<Optional<TResult>>> mapper)
        => ConvertOptionalTask<TResult, Supplier<T, Task<Optional<TResult>>>>(mapper);

    /// <inheritdoc cref="IFunctional{TDelegate}.ToDelegate()"/>
    Func<object?> IFunctional<Func<object?>>.ToDelegate() => Func.Constant<object?>(kind is NotEmptyValue ? value : null);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Optional<T> If<TPredicate>(TPredicate condition)
        where TPredicate : struct, ISupplier<T, bool>
        => HasValue && condition.Invoke(value) ? this : None;

    /// <summary>
    /// If a value is present, and the value matches the given predicate,
    /// return an Optional describing the value, otherwise return an empty Optional.
    /// </summary>
    /// <param name="condition">A predicate to apply to the value, if present.</param>
    /// <returns>An Optional describing the value of this Optional if a value is present and the value matches the given predicate, otherwise an empty Optional.</returns>
    public Optional<T> If(Predicate<T> condition) => If<DelegatingPredicate<T>>(condition);

    /// <summary>
    /// If a value is present, and the value matches the given predicate,
    /// return an Optional describing the value, otherwise return an empty Optional.
    /// </summary>
    /// <param name="condition">A predicate to apply to the value, if present.</param>
    /// <returns>An Optional describing the value of this Optional if a value is present and the value matches the given predicate, otherwise an empty Optional.</returns>
    [CLSCompliant(false)]
    public unsafe Optional<T> If(delegate*<T, bool> condition) => If<Supplier<T, bool>>(condition);

    /// <summary>
    /// Returns textual representation of this object.
    /// </summary>
    /// <returns>The textual representation of this object.</returns>
    public override string? ToString() => kind switch
    {
        UndefinedValue => "<Undefined>",
        NullValue => "<Null>",
        _ => value!.ToString()
    };

    /// <summary>
    /// Computes hash code of the stored value.
    /// </summary>
    /// <returns>The hash code of the stored value.</returns>
    /// <remarks>
    /// This method uses <see cref="EqualityComparer{T}"/> type
    /// to get hash code of <see cref="Value"/>.
    /// </remarks>
    public override int GetHashCode() => HasValue ? EqualityComparer<T>.Default.GetHashCode(value) : kind;

    /// <summary>
    /// Determines whether this container stored the same
    /// value as the specified value.
    /// </summary>
    /// <param name="other">Other value to compare.</param>
    /// <returns><see langword="true"/> if <see cref="Value"/> is equal to <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
    public bool Equals(T? other) => !IsUndefined && EqualityComparer<T?>.Default.Equals(value, other);

    private bool Equals(in Optional<T> other)
        => kind == other.kind && (kind is UndefinedValue or NullValue || EqualityComparer<T?>.Default.Equals(value, other.value));

    /// <summary>
    /// Determines whether this container stores
    /// the same value as the specified one.
    /// </summary>
    /// <param name="other">The container to compare.</param>
    /// <returns><see langword="true"/> if this container stores the same value as <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
    public bool Equals(Optional<T> other) => Equals(in other);

    /// <summary>
    /// Determines whether this container stores
    /// the same value as the specified one.
    /// </summary>
    /// <param name="other">The container to compare.</param>
    /// <returns><see langword="true"/> if this container stores the same value as <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
    public override bool Equals(object? other) => other switch
    {
        null => IsNull,
        Optional<T> optional => Equals(in optional),
        T value => Equals(value),
        _ => ReferenceEquals(other, Sentinel.Instance) && IsUndefined,
    };

    /// <summary>
    /// Performs equality check between stored value
    /// and the specified value using method <see cref="IEqualityComparer.Equals(object, object)"/>.
    /// </summary>
    /// <param name="other">Other object to compare with <see cref="Value"/>.</param>
    /// <param name="comparer">The comparer implementing custom equality check.</param>
    /// <returns><see langword="true"/> if <paramref name="other"/> is equal to <see cref="Value"/> using custom check; otherwise, <see langword="false"/>.</returns>
    public bool Equals(object? other, IEqualityComparer comparer)
        => !IsUndefined && comparer.Equals(value, other);

    /// <summary>
    /// Computes hash code for the stored value
    /// using method <see cref="IEqualityComparer.GetHashCode(object)"/>.
    /// </summary>
    /// <param name="comparer">The comparer implementing hash code function.</param>
    /// <returns>The hash code of <see cref="Value"/>.</returns>
    public int GetHashCode(IEqualityComparer comparer) => HasValue ? comparer.GetHashCode(value) : kind;

    /// <summary>
    /// Wraps value into Optional container.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Optional<T>(T? value) => new(value);

    /// <summary>
    /// Extracts value stored in the Optional container.
    /// </summary>
    /// <param name="optional">The container.</param>
    /// <exception cref="InvalidOperationException">No value is present.</exception>
    public static explicit operator T(in Optional<T> optional) => optional.Value;

    /// <summary>
    /// Determines whether two containers store the same value.
    /// </summary>
    /// <param name="first">The first container to compare.</param>
    /// <param name="second">The second container to compare.</param>
    /// <returns><see langword="true"/>, if both containers store the same value; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(in Optional<T> first, in Optional<T> second)
        => first.Equals(in second);

    /// <summary>
    /// Determines whether two containers store the different values.
    /// </summary>
    /// <param name="first">The first container to compare.</param>
    /// <param name="second">The second container to compare.</param>
    /// <returns><see langword="true"/>, if both containers store the different values; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(in Optional<T> first, in Optional<T> second)
        => !first.Equals(in second);

    /// <summary>
    /// Returns non-empty container.
    /// </summary>
    /// <param name="first">The first container.</param>
    /// <param name="second">The second container.</param>
    /// <returns>The first non-empty container.</returns>
    /// <seealso cref="Optional.Coalesce{T}"/>
    public static Optional<T> operator |(in Optional<T> first, in Optional<T> second)
        => first.HasValue ? first : second;

    /// <summary>
    /// Returns the value if present; otherwise return default value.
    /// </summary>
    /// <param name="optional">The optional value.</param>
    /// <param name="defaultValue">The value to be returned if there is no value present.</param>
    /// <returns>The value, if present, otherwise <paramref name="defaultValue"/>.</returns>
    [return: NotNullIfNotNull(nameof(defaultValue))]
    public static T? operator |(in Optional<T> optional, T? defaultValue)
        => optional.Or(defaultValue);

    /// <summary>
    /// Determines whether two containers are empty or have values.
    /// </summary>
    /// <param name="first">The first container.</param>
    /// <param name="second">The second container.</param>
    /// <returns><see cref="None"/>, if both containers are empty or have values; otherwise, non-empty container.</returns>
    public static Optional<T> operator ^(in Optional<T> first, in Optional<T> second) => (first.kind - second.kind) switch
    {
        UndefinedValue - NullValue or NullValue - NotEmptyValue or UndefinedValue - NotEmptyValue => second,
        NotEmptyValue - UndefinedValue or NotEmptyValue - NullValue or NullValue - UndefinedValue => first,
        _ => None
    };

    /// <inheritdoc cref="IOptionMonad{T,TSelf}.op_True"/>
    public static bool operator true(in Optional<T> optional) => optional.HasValue;

    /// <inheritdoc cref="IOptionMonad{T,TSelf}.op_False"/>
    public static bool operator false(in Optional<T> optional) => !optional;

    /// <inheritdoc cref="IOptionMonad{T,TSelf}.op_LogicalNot"/>
    public static bool operator !(in Optional<T> optional) => optional.kind < NotEmptyValue;
}