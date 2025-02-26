using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace DotNext;

using System;
using DotNext.Threading.Tasks;
using Runtime.CompilerServices;
using Intrinsics = Runtime.Intrinsics;

/// <summary>
/// Represents extension methods for type <see cref="Result{T}"/>.
/// </summary>
public static class Result
{
    /// <summary>
    /// If a result is successful, returns it, otherwise <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <returns>Nullable value.</returns>
    public static T? OrNull<T>(this in Result<T> result)
        where T : struct
        => result.TryGet(out var value) ? value : null;

    /// <summary>
    /// If a result is successful, returns it, otherwise <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">Value type.</typeparam>
    /// <typeparam name="TError">The type of the error code.</typeparam>
    /// <param name="result">The result.</param>
    /// <returns>Nullable value.</returns>
    public static T? OrNull<T, TError>(this in Result<T, TError> result)
        where T : struct
        where TError : struct, Enum
        => result.TryGet(out var value) ? value : null;

    /// <summary>
    /// Returns the second result if the first is unsuccessful.
    /// </summary>
    /// <param name="first">The first result.</param>
    /// <param name="second">The second result.</param>
    /// <typeparam name="T">The type of value.</typeparam>
    /// <returns>The second result if the first is unsuccessful; otherwise, the first result.</returns>
    public static ref readonly Result<T> Coalesce<T>(this in Result<T> first, in Result<T> second) => ref first.IsSuccessful ? ref first : ref second;

    /// <summary>
    /// Indicates that specified type is result type.
    /// </summary>
    /// <param name="resultType">The type of <see cref="Result{T}"/>.</param>
    /// <returns><see langword="true"/>, if specified type is result type; otherwise, <see langword="false"/>.</returns>
    public static bool IsResult(this Type resultType) => resultType.IsConstructedGenericType && resultType.GetGenericTypeDefinition().IsOneOf([typeof(Result<>), typeof(Result<,>)]);

    /// <summary>
    /// Returns the underlying type argument of the specified result type.
    /// </summary>
    /// <param name="resultType">Result type.</param>
    /// <returns>Underlying type argument of result type; otherwise, <see langword="null"/>.</returns>
    public static Type? GetUnderlyingType(Type resultType) => IsResult(resultType) ? resultType.GetGenericArguments()[0] : null;

    /// <summary>
    /// Creates a new instance of <see cref="Result{T}"/> from the specified value.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value to be placed to the container.</param>
    /// <returns>The value encapsulated by <see cref="Result{T}"/>.</returns>
    public static Result<T> FromValue<T>(T value) => new(value);

    /// <summary>
    /// Creates a new instance of <see cref="Result{T}"/> from the specified exception.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="e">The exception to be placed to the container.</param>
    /// <returns>The exception encapsulated by <see cref="Result{T}"/>.</returns>
    public static Result<T> FromException<T>(Exception e) => new(e);

    /// <summary>
    /// Creates a new instance of <see cref="Result{T, TError}"/> from the specified exception.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <typeparam name="TError">The type of the error code. Default value must represent the successful result.</typeparam>
    /// <param name="e">The error to be placed to the container.</param>
    /// <returns>The exception encapsulated by <see cref="Result{T}"/>.</returns>
    public static Result<T, TError> FromError<T, TError>(TError e)
        where TError: struct, Enum
        => new(e);

    private static AwaitableResult<TResult> TransformAwaitableResult<T, TResult>(this AwaitableResult<T> task, Converter<Result<T>, Result<TResult>> converter)
    {
        async Task<TResult> ConvertInternal()
        {
            var result = await task.ConfigureAwait(false);
            var conversionResult = converter(result);
            return conversionResult.IsSuccessful ? conversionResult.Value : throw conversionResult.Error;
        }

        return ConvertInternal().SuspendException();
    }

    private static AwaitableResult<TResult> TransformAwaitableResult<T, TResult>(this AwaitableResult<T> task, Converter<Result<T>, AwaitableResult<TResult>> converter)
    {
        async Task<TResult> ConvertInternal()
        {
            var result = await task.ConfigureAwait(false);
            var conversionResult = await converter(result);
            return conversionResult.IsSuccessful ? conversionResult.Value : throw conversionResult.Error;
        }

        return ConvertInternal().SuspendException();
    }

    /// <summary>
    /// If successful result is present, apply the provided mapping function hiding any exception
    /// caused by the converter.
    /// </summary>
    /// <param name="task">The task containing Result value.</param>
    /// <param name="converter">A mapping function to be applied to the value, if present.</param>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <typeparam name="TResult">The type of the result of the mapping function.</typeparam>
    /// <returns>The conversion result.</returns>
    public static AwaitableResult<TResult> Convert<T, TResult>(this AwaitableResult<T> task, Converter<T, TResult> converter)
        => task.TransformAwaitableResult((result) => result.Convert(converter));

    /// <summary>
    /// If successful result is present, apply the provided mapping function. If not,
    /// forward the exception.
    /// </summary>
    /// <param name="task">The task containing Result value.</param>
    /// <param name="converter">A mapping function to be applied to the value, if present.</param>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <typeparam name="TResult">The type of the result of the mapping function.</typeparam>
    /// <returns>The conversion result.</returns>
    public static AwaitableResult<TResult> Convert<T, TResult>(this AwaitableResult<T> task, Converter<T, Result<TResult>> converter)
        => task.TransformAwaitableResult((result) => result.Convert(converter));

    /// <summary>
    /// If successful result is present, apply the provided mapping function. If not,
    /// forward the exception.
    /// </summary>
    /// <param name="task">The task containing Result value.</param>
    /// <param name="converter">A mapping function to be applied to the value, if present.</param>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <typeparam name="TResult">The type of the result of the mapping function.</typeparam>
    /// <returns>The conversion result.</returns>
    public static AwaitableResult<TResult> Convert<T, TResult>(this AwaitableResult<T> task, Converter<T, Task<TResult>> converter)
        => task.TransformAwaitableResult((result) => result.Convert(converter));

    /// <summary>
    /// If successful result is present, apply the provided mapping function. If not,
    /// forward the exception.
    /// </summary>
    /// <param name="task">The task containing Result value.</param>
    /// <param name="converter">A mapping function to be applied to the value, if present.</param>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <typeparam name="TResult">The type of the result of the mapping function.</typeparam>
    /// <returns>The conversion result.</returns>
    public static AwaitableResult<TResult> Convert<T, TResult>(this AwaitableResult<T> task, Converter<T, Task<Result<TResult>>> converter)
        => task.TransformAwaitableResult((result) => result.Convert(converter));

    /// <summary>
    /// Converts the result into <see cref="Optional{T}"/>.
    /// </summary>
    /// <returns>Option monad representing value in this monad.</returns>
    public static async Task<Optional<T>> TryGet<T>(this Task<Result<T>> task)
        => (await task.ConfigureAwait(false)).TryGet();

    /// <summary>
    /// Converts the awaitable Result into a task holding <see cref="Optional{T}"/>.
    /// </summary>
    /// <returns>A task holding an Option monad representing value in this monad.</returns>
    public static async Task<Optional<T>> TryGet<T>(this AwaitableResult<T> awaitableResult)
        => (await awaitableResult.ConfigureAwait(false)).TryGet();

    /// <summary>
    /// Converts the result into <see cref="Optional{T}"/>.
    /// </summary>
    /// <returns>Option monad representing value in this monad.</returns>
    public static async Task<Optional<T>> TryGet<T, TError>(this Task<Result<T, TError>> task)
        where TError : struct, Enum
        => (await task.ConfigureAwait(false)).TryGet();

    /// <summary>
    /// Converts this task containing a Result to <see cref="AwaitableResult{TResult}"/>.
    /// </summary>
    /// <returns>The completed task representing the result.</returns>
    public static AwaitableResult<T> ToAwaitable<T>(this Task<Result<T>> task)
    {
        async Task<T> ConvertInternal()
        {
            var result = await task.ConfigureAwait(false);
            return result.IsSuccessful ? result.Value : throw result.Error;
        }

        return ConvertInternal().SuspendException();
    }
}

/// <summary>
/// Represents a result of operation which can be actual result or exception.
/// </summary>
/// <typeparam name="T">The type of the value stored in the Result monad.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly struct Result<T> : IResultMonad<T, Exception, Result<T>>
{
    private readonly T value;
    private readonly ExceptionDispatchInfo? exception;

    /// <summary>
    /// Initializes a new successful result.
    /// </summary>
    /// <param name="value">The value to be stored as result.</param>
    public Result(T value) => this.value = value;

    /// <summary>
    /// Initializes a new unsuccessful result.
    /// </summary>
    /// <param name="error">The exception representing error. Cannot be <see langword="null"/>.</param>
    public Result(Exception error)
        : this(ExceptionDispatchInfo.Capture(error))
    {
    }

    private Result(ExceptionDispatchInfo dispatchInfo)
    {
        Unsafe.SkipInit(out value);
        exception = dispatchInfo;
    }

    /// <summary>
    /// Initializes a new unsuccessful result.
    /// </summary>
    /// <param name="error">The exception representing error. Cannot be <see langword="null"/>.</param>
    /// <returns>The unsuccessful result.</returns>
    static Result<T> IResultMonad<T, Exception, Result<T>>.FromError(Exception error) => new(error);

    /// <summary>
    /// Creates <see cref="Result{T}"/> from <see cref="Optional{T}"/> instance.
    /// </summary>
    /// <param name="optional">The optional value.</param>
    /// <returns>The converted optional value.</returns>
    public static Result<T> FromOptional(in Optional<T> optional) => optional switch
    {
        { HasValue: true } => new(optional.ValueOrDefault),
        { IsNull: true } => default,
        _ => new(new InvalidOperationException(ExceptionMessages.OptionalNoValue))
    };

    /// <summary>
    /// Indicates that the result is successful.
    /// </summary>
    /// <value><see langword="true"/> if this result is successful; <see langword="false"/> if this result represents exception.</value>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccessful => exception is null;

    /// <inheritdoc />
    bool IOptionMonad<T>.HasValue => IsSuccessful;

    /// <summary>
    /// Extracts the actual result.
    /// </summary>
    /// <exception cref="Exception">This result is not successful.</exception>
    public T Value
    {
        get
        {
            Validate();
            return value;
        }
    }

    /// <summary>
    /// Gets a reference to the underlying value.
    /// </summary>
    /// <value>The reference to the result.</value>
    /// <exception cref="Exception">The result is unavailable.</exception>
    [UnscopedRef]
    [JsonIgnore]
    public ref readonly T ValueRef
    {
        get
        {
            Validate();
            return ref value;
        }
    }

    /// <summary>
    /// Gets the value if present; otherwise return default value.
    /// </summary>
    /// <value>The value, if present, otherwise <c>default</c>.</value>
    public T? ValueOrDefault => value;

    private void Validate() => exception?.Throw();

    /// <inheritdoc />
    object? ISupplier<object?>.Invoke() => IsSuccessful ? value : null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Result<TResult> Convert<TResult, TConverter>(TConverter converter)
        where TConverter : struct, ISupplier<T, TResult>
    {
        Result<TResult> result;
        if (exception is null)
        {
            try
            {
                result = converter.Invoke(value);
            }
            catch (Exception e)
            {
                result = new(e);
            }
        }
        else
        {
            result = new(exception);
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Result<TResult> ConvertResult<TResult, TConverter>(TConverter converter)
        where TConverter : struct, ISupplier<T, Result<TResult>>
    {
        Result<TResult> result;
        if (exception is null)
        {
            try
            {
                result = converter.Invoke(value);
            }
            catch (Exception e)
            {
                result = new(e);
            }
        }
        else
        {
            result = new(exception);
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private AwaitableResult<TResult> ConvertTask<TResult, TConverter>(TConverter converter)
        where TConverter : struct, ISupplier<T, Task<TResult>>
    {
        AwaitableResult<TResult> result;
        if (exception is null)
        {
            try
            {
                result = converter.Invoke(value).SuspendException();
            }
            catch (Exception e)
            {
                result = new(Task.FromException<TResult>(e));
            }
        }
        else
        {
            result = new(Task.FromException<TResult>(exception.SourceException));
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private AwaitableResult<TResult> ConvertResultTask<TResult, TConverter>(TConverter converter)
        where TConverter : struct, ISupplier<T, Task<Result<TResult>>>
    {
        AwaitableResult<TResult> result;
        if (exception is null)
        {
            var valueCopy = value;
            async Task<TResult> GetConversionResult()
            {
                var conversionResult = await converter.Invoke(valueCopy).ConfigureAwait(false);
                return conversionResult.IsSuccessful ? conversionResult.Value : throw conversionResult.Error;
            }

            result = new(GetConversionResult());
        }
        else
        {
            result = new(Task.FromException<TResult>(exception.SourceException));
        }

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private AwaitableResult<TResult> ConvertAwaitableResult<TResult, TConverter>(TConverter converter)
        where TConverter : struct, ISupplier<T, AwaitableResult<TResult>>
    {
        AwaitableResult<TResult> result;
        if (exception is null)
        {
            var valueCopy = value;
            async Task<TResult> GetConversionResult()
            {
                var conversionResult = await converter.Invoke(valueCopy).ConfigureAwait(false);
                return conversionResult.IsSuccessful ? conversionResult.Value : throw conversionResult.Error;
            }

            result = new(GetConversionResult());
        }
        else
        {
            result = new(Task.FromException<TResult>(exception.SourceException));
        }

        return result;
    }

    /// <summary>
    /// If successful result is present, apply the provided mapping function hiding any exception
    /// caused by the converter.
    /// </summary>
    /// <param name="converter">A mapping function to be applied to the value, if present.</param>
    /// <typeparam name="TResult">The type of the result of the mapping function.</typeparam>
    /// <returns>The conversion result.</returns>
    public Result<TResult> Convert<TResult>(Converter<T, TResult> converter)
        => Convert<TResult, DelegatingConverter<T, TResult>>(converter);

    /// <summary>
    /// If successful result is present, apply the provided mapping function. If not,
    /// forward the exception.
    /// </summary>
    /// <param name="converter">A mapping function to be applied to the value, if present.</param>
    /// <typeparam name="TResult">The type of the result of the mapping function.</typeparam>
    /// <returns>The conversion result.</returns>
    public Result<TResult> Convert<TResult>(Converter<T, Result<TResult>> converter)
        => ConvertResult<TResult, DelegatingConverter<T, Result<TResult>>>(converter);

    /// <summary>
    /// If successful result is present, apply the provided mapping function. If not,
    /// forward the exception.
    /// </summary>
    /// <param name="converter">A mapping function to be applied to the value, if present.</param>
    /// <typeparam name="TResult">The type of the result of the mapping function.</typeparam>
    /// <returns>The conversion result.</returns>
    public AwaitableResult<TResult> Convert<TResult>(Converter<T, Task<TResult>> converter)
        => ConvertTask<TResult, DelegatingConverter<T, Task<TResult>>>(converter);

    /// <summary>
    /// If successful result is present, apply the provided mapping function. If not,
    /// forward the exception.
    /// </summary>
    /// <param name="converter">A mapping function to be applied to the value, if present.</param>
    /// <typeparam name="TResult">The type of the result of the mapping function.</typeparam>
    /// <returns>The conversion result.</returns>
    public AwaitableResult<TResult> Convert<TResult>(Converter<T, Task<Result<TResult>>> converter)
        => ConvertResultTask<TResult, DelegatingConverter<T, Task<Result<TResult>>>>(converter);

    /// <summary>
    /// If successful result is present, apply the provided mapping function. If not,
    /// forward the exception.
    /// </summary>
    /// <param name="converter">A mapping function to be applied to the value, if present.</param>
    /// <typeparam name="TResult">The type of the result of the mapping function.</typeparam>
    /// <returns>The conversion result.</returns>
    public AwaitableResult<TResult> Convert<TResult>(Converter<T, AwaitableResult<TResult>> converter)
        => ConvertAwaitableResult<TResult, DelegatingConverter<T, AwaitableResult<TResult>>>(converter);

    /// <summary>
    /// If successful result is present, apply the provided mapping function hiding any exception
    /// caused by the converter.
    /// </summary>
    /// <param name="converter">A mapping function to be applied to the value, if present.</param>
    /// <typeparam name="TResult">The type of the result of the mapping function.</typeparam>
    /// <returns>The conversion result.</returns>
    [CLSCompliant(false)]
    public unsafe Result<TResult> Convert<TResult>(delegate*<T, TResult> converter)
        => Convert<TResult, Supplier<T, TResult>>(converter);

    /// <summary>
    /// If successful result is present, apply the provided mapping function. If not,
    /// forward the exception.
    /// </summary>
    /// <param name="converter">A mapping function to be applied to the value, if present.</param>
    /// <typeparam name="TResult">The type of the result of the mapping function.</typeparam>
    /// <returns>The conversion result.</returns>
    [CLSCompliant(false)]
    public unsafe Result<TResult> Convert<TResult>(delegate*<T, Result<TResult>> converter)
        => ConvertResult<TResult, Supplier<T, Result<TResult>>>(converter);

    /// <summary>
    /// If successful result is present, apply the provided mapping function. If not,
    /// forward the exception.
    /// </summary>
    /// <param name="converter">A mapping function to be applied to the value, if present.</param>
    /// <typeparam name="TResult">The type of the result of the mapping function.</typeparam>
    /// <returns>The conversion result.</returns>
    [CLSCompliant(false)]
    public unsafe AwaitableResult<TResult> Convert<TResult>(delegate*<T, Task<TResult>> converter)
        => ConvertTask<TResult, Supplier<T, Task<TResult>>>(converter);

    /// <summary>
    /// If successful result is present, apply the provided mapping function. If not,
    /// forward the exception.
    /// </summary>
    /// <param name="converter">A mapping function to be applied to the value, if present.</param>
    /// <typeparam name="TResult">The type of the result of the mapping function.</typeparam>
    /// <returns>The conversion result.</returns>
    [CLSCompliant(false)]
    public unsafe AwaitableResult<TResult> Convert<TResult>(delegate*<T, Task<Result<TResult>>> converter)
        => ConvertResultTask<TResult, Supplier<T, Task<Result<TResult>>>>(converter);

    /// <summary>
    /// If successful result is present, apply the provided mapping function. If not,
    /// forward the exception.
    /// </summary>
    /// <param name="converter">A mapping function to be applied to the value, if present.</param>
    /// <typeparam name="TResult">The type of the result of the mapping function.</typeparam>
    /// <returns>The conversion result.</returns>
    [CLSCompliant(false)]
    public unsafe AwaitableResult<TResult> Convert<TResult>(delegate*<T, AwaitableResult<TResult>> converter)
        => ConvertAwaitableResult<TResult, Supplier<T, AwaitableResult<TResult>>>(converter);

    /// <summary>
    /// Attempts to extract value from container if it is present.
    /// </summary>
    /// <param name="value">Extracted value.</param>
    /// <returns><see langword="true"/> if value is present; otherwise, <see langword="false"/>.</returns>
    public bool TryGet(out T value)
    {
        value = this.value;
        return exception is null;
    }

    /// <summary>
    /// Returns the value if present; otherwise return default value.
    /// </summary>
    /// <param name="defaultValue">The value to be returned if this result is unsuccessful.</param>
    /// <returns>The value, if present, otherwise <paramref name="defaultValue"/>.</returns>
    public T? Or(T? defaultValue) => exception is null ? value : defaultValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private T OrInvoke<TSupplier>(TSupplier defaultFunc)
        where TSupplier : struct, ISupplier<T>
        => exception is null ? value : defaultFunc.Invoke();

    /// <summary>
    /// Returns the value if present; otherwise invoke delegate.
    /// </summary>
    /// <param name="defaultFunc">A delegate to be invoked if value is not present.</param>
    /// <returns>The value, if present, otherwise returned from delegate.</returns>
    public T OrInvoke(Func<T> defaultFunc)
        => OrInvoke<DelegatingSupplier<T>>(defaultFunc);

    /// <summary>
    /// Returns the value if present; otherwise invoke delegate.
    /// </summary>
    /// <param name="defaultFunc">A delegate to be invoked if value is not present.</param>
    /// <returns>The value, if present, otherwise returned from delegate.</returns>
    [CLSCompliant(false)]
    public unsafe T OrInvoke(delegate*<T> defaultFunc)
        => OrInvoke<Supplier<T>>(defaultFunc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private T OrInvokeWithException<TSupplier>(TSupplier defaultFunc)
        where TSupplier : struct, ISupplier<Exception, T>
        => exception is null ? value : defaultFunc.Invoke(exception.SourceException);

    /// <summary>
    /// Returns the value if present; otherwise invoke delegate.
    /// </summary>
    /// <param name="defaultFunc">A delegate to be invoked if value is not present.</param>
    /// <returns>The value, if present, otherwise returned from delegate.</returns>
    public T OrInvoke(Func<Exception, T> defaultFunc)
        => OrInvokeWithException<DelegatingSupplier<Exception, T>>(defaultFunc);

    /// <summary>
    /// Returns the value if present; otherwise invoke delegate.
    /// </summary>
    /// <param name="defaultFunc">A delegate to be invoked if value is not present.</param>
    /// <returns>The value, if present, otherwise returned from delegate.</returns>
    [CLSCompliant(false)]
    public unsafe T OrInvoke(delegate*<Exception, T> defaultFunc)
        => OrInvokeWithException<Supplier<Exception, T>>(defaultFunc);

    /// <inheritdoc cref="IFunctional{TDelegate}.ToDelegate()"/>
    Func<object?> IFunctional<Func<object?>>.ToDelegate() => Func.Constant<object?>(exception is null ? value : null);

    /// <summary>
    /// Gets exception associated with this result.
    /// </summary>
    public Exception? Error => exception?.SourceException;

    /// <summary>
    /// Returns the value if present; otherwise return default value.
    /// </summary>
    /// <param name="result">The result to check.</param>
    /// <param name="defaultValue">The value to be returned if this result is unsuccessful.</param>
    /// <returns>The value, if present, otherwise <paramref name="defaultValue"/>.</returns>
    public static T? operator |(in Result<T> result, T? defaultValue)
        => result.Or(defaultValue);

    /// <summary>
    /// Tries to return successful result.
    /// </summary>
    /// <param name="x">The first container.</param>
    /// <param name="y">The second container.</param>
    /// <returns>The first successful result.</returns>
    public static Result<T> operator |(in Result<T> x, in Result<T> y)
        => x.IsSuccessful ? x : y;

    /// <summary>
    /// Converts this result to <see cref="Task{TResult}"/>.
    /// </summary>
    /// <returns>The completed task representing the result.</returns>
    public ValueTask<T> AsTask()
        => exception?.SourceException switch
        {
            null => new(value),
            OperationCanceledException canceledEx => ValueTask.FromCanceled<T>(canceledEx.CancellationToken),
            { } error => ValueTask.FromException<T>(error),
        };

    /// <summary>
    /// Converts this result to <see cref="AwaitableResult{TResult}"/>.
    /// </summary>
    /// <returns>The awaitable Result representing the result.</returns>
    public AwaitableResult<T> ToAwaitable()
        => IsSuccessful ? new(Task.FromResult(value)) : new(Task.FromException<T>(Error));

    /// <summary>
    /// Converts the result to <see cref="Task{TResult}"/>.
    /// </summary>
    /// <param name="result">The result to be converted.</param>
    /// <returns>The completed task representing the result.</returns>
    public static explicit operator ValueTask<T>(in Result<T> result) => result.AsTask();

    /// <summary>
    /// Gets boxed representation of the result.
    /// </summary>
    /// <returns>The boxed representation of the result.</returns>
    public Result<object?> Box() => exception is null ? new(value) : new(exception);

    /// <summary>
    /// Extracts actual result.
    /// </summary>
    /// <param name="result">The result object.</param>
    public static explicit operator T(in Result<T> result) => result.Value;

    /// <summary>
    /// Converts the result into <see cref="Optional{T}"/>.
    /// </summary>
    /// <returns>Option monad representing value in this monad.</returns>
    public Optional<T> TryGet() => exception is null ? new(value) : Optional<T>.None;

    /// <summary>
    /// Converts the result into <see cref="Optional{T}"/>.
    /// </summary>
    /// <param name="result">The result to be converted.</param>
    /// <returns>Option monad representing value in this monad.</returns>
    public static implicit operator Optional<T>(in Result<T> result) => result.TryGet();

    /// <summary>
    /// Converts value into the result.
    /// </summary>
    /// <param name="result">The result to be converted.</param>
    /// <returns>The result representing <paramref name="result"/> value.</returns>
    public static implicit operator Result<T>(T result) => new(result);

    /// <summary>
    /// Converts <see cref="Optional{T}"/> to <see cref="Result{T}"/>.
    /// </summary>
    /// <param name="optional">The optional value.</param>
    /// <returns>The result representing optional value.</returns>
    public static explicit operator Result<T>(in Optional<T> optional) => FromOptional(in optional);

    /// <summary>
    /// Indicates that both results are successful.
    /// </summary>
    /// <param name="left">The first result to check.</param>
    /// <param name="right">The second result to check.</param>
    /// <returns><see langword="true"/> if both results are successful; otherwise, <see langword="false"/>.</returns>
    public static bool operator &(in Result<T> left, in Result<T> right) => left.exception is null && right.exception is null;

    /// <summary>
    /// Indicates that the result is successful.
    /// </summary>
    /// <param name="result">The result to check.</param>
    /// <returns><see langword="true"/> if this result is successful; <see langword="false"/> if this result represents exception.</returns>
    public static bool operator true(in Result<T> result) => result.exception is null;

    /// <summary>
    /// Indicates that the result represents error.
    /// </summary>
    /// <param name="result">The result to check.</param>
    /// <returns><see langword="false"/> if this result is successful; <see langword="true"/> if this result represents exception.</returns>
    public static bool operator false(in Result<T> result) => result.exception is not null;

    /// <summary>
    /// Returns textual representation of this object.
    /// </summary>
    /// <returns>The textual representation of this object.</returns>
    public override string ToString() => exception?.SourceException.ToString() ?? value?.ToString() ?? "<NULL>";
}

/// <summary>
/// Represents a result of operation which can be actual result or error code.
/// </summary>
/// <typeparam name="T">The type of the result.</typeparam>
/// <typeparam name="TError">
/// The type of the error code. Default value must represent the successful result.
/// </typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly struct Result<T, TError> : IResultMonad<T, TError, Result<T, TError>>
    where TError : struct, Enum
{
    private readonly T value;
    private readonly TError errorCode;

    /// <summary>
    /// Initializes a new successful result.
    /// </summary>
    /// <param name="value">The result value.</param>
    public Result(T value)
    {
        this.value = value;
    }

    /// <summary>
    /// Initializes a new unsuccessful result.
    /// </summary>
    /// <param name="error">The error code.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="error"/> represents a successful code.</exception>
    public Result(TError error)
    {
        Unsafe.SkipInit(out value);
        errorCode = Intrinsics.IsDefault(in error) ? throw new ArgumentOutOfRangeException(nameof(error)) : error;
    }

    /// <summary>
    /// Initializes a new unsuccessful result.
    /// </summary>
    /// <param name="error">The error code.</param>
    /// <returns>The unsuccessful result.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="error"/> represents a successful code.</exception>
    static Result<T, TError> IResultMonad<T, TError, Result<T, TError>>.FromError(TError error) => new(error);

    /// <summary>
    /// Gets boxed representation of the result.
    /// </summary>
    /// <returns>The boxed representation of the result.</returns>
    public Result<object?, TError> Box() => IsSuccessful ? new(value) : new(errorCode);

    /// <summary>
    /// Extracts the actual result.
    /// </summary>
    /// <exception cref="UndefinedResultException{TError}">The value is unavailable.</exception>
    public T Value
    {
        get
        {
            Validate();
            return value;
        }
    }

    /// <summary>
    /// Gets a reference to the underlying value.
    /// </summary>
    /// <value>The reference to the result.</value>
    /// <exception cref="UndefinedResultException{TError}">The value is unavailable.</exception>
    [UnscopedRef]
    [JsonIgnore]
    public ref readonly T ValueRef
    {
        get
        {
            Validate();
            return ref value;
        }
    }

    /// <summary>
    /// Returns the value if present; otherwise return default value.
    /// </summary>
    /// <returns>The value, if present, otherwise <c>default</c>.</returns>
    public T? ValueOrDefault => value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Validate()
    {
        if (!IsSuccessful)
            Throw();
    }

    [StackTraceHidden]
    [DoesNotReturn]
    private void Throw() => throw new UndefinedResultException<TError>(Error);

    /// <inheritdoc />
    object? ISupplier<object?>.Invoke() => IsSuccessful ? value : null;

    /// <summary>
    /// Gets the error code.
    /// </summary>
    public TError Error => errorCode;

    /// <summary>
    /// Indicates that the result is successful.
    /// </summary>
    /// <value><see langword="true"/> if this result is successful; <see langword="false"/> if this result represents exception.</value>
    public bool IsSuccessful => Intrinsics.IsDefault(in errorCode);

    /// <inheritdoc />
    bool IOptionMonad<T>.HasValue => IsSuccessful;

    /// <summary>
    /// Attempts to extract value from container if it is present.
    /// </summary>
    /// <param name="value">Extracted value.</param>
    /// <returns><see langword="true"/> if value is present; otherwise, <see langword="false"/>.</returns>
    public bool TryGet(out T value)
    {
        value = this.value;
        return IsSuccessful;
    }

    /// <summary>
    /// Converts the result into <see cref="Optional{T}"/>.
    /// </summary>
    /// <returns>Option monad representing value in this monad.</returns>
    public Optional<T> TryGet() => IsSuccessful ? new(value) : Optional<T>.None;

    /// <summary>
    /// Returns the value if present; otherwise return default value.
    /// </summary>
    /// <param name="defaultValue">The value to be returned if this result is unsuccessful.</param>
    /// <returns>The value, if present, otherwise <paramref name="defaultValue"/>.</returns>
    public T? Or(T? defaultValue) => IsSuccessful ? value : defaultValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Result<TResult, TError> Convert<TResult, TConverter>(TConverter converter)
        where TConverter : struct, ISupplier<T, TResult>
        => IsSuccessful ? new(converter.Invoke(value)) : new(Error);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Result<TResult, TError> ConvertResult<TResult, TConverter>(TConverter converter)
        where TConverter : struct, ISupplier<T, Result<TResult, TError>>
        => IsSuccessful ? converter.Invoke(value) : new(Error);

    /// <summary>
    /// If successful result is present, apply the provided mapping function hiding any exception
    /// caused by the converter.
    /// </summary>
    /// <param name="converter">A mapping function to be applied to the value, if present.</param>
    /// <typeparam name="TResult">The type of the result of the mapping function.</typeparam>
    /// <returns>The conversion result.</returns>
    public Result<TResult, TError> Convert<TResult>(Converter<T, TResult> converter)
        => Convert<TResult, DelegatingConverter<T, TResult>>(converter);

    /// <summary>
    /// If successful result is present, apply the provided mapping function. If not,
    /// forward the error.
    /// </summary>
    /// <param name="converter">A mapping function to be applied to the value, if present.</param>
    /// <typeparam name="TResult">The type of the result of the mapping function.</typeparam>
    /// <returns>The conversion result.</returns>
    public Result<TResult, TError> Convert<TResult>(Converter<T, Result<TResult, TError>> converter)
        => ConvertResult<TResult, DelegatingConverter<T, Result<TResult,TError>>>(converter);

    /// <summary>
    /// If successful result is present, apply the provided mapping function hiding any exception
    /// caused by the converter.
    /// </summary>
    /// <param name="converter">A mapping function to be applied to the value, if present.</param>
    /// <typeparam name="TResult">The type of the result of the mapping function.</typeparam>
    /// <returns>The conversion result.</returns>
    [CLSCompliant(false)]
    public unsafe Result<TResult, TError> Convert<TResult>(delegate*<T, TResult> converter)
        => Convert<TResult, Supplier<T, TResult>>(converter);

    /// <summary>
    /// If successful result is present, apply the provided mapping function. If not,
    /// forward the error.
    /// </summary>
    /// <param name="converter">A mapping function to be applied to the value, if present.</param>
    /// <typeparam name="TResult">The type of the result of the mapping function.</typeparam>
    /// <returns>The conversion result.</returns>
    [CLSCompliant(false)]
    public unsafe Result<TResult, TError> Convert<TResult>(delegate*<T, Result<TResult, TError>> converter)
        => ConvertResult<TResult, Supplier<T, Result<TResult, TError>>>(converter);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private T OrInvoke<TSupplier>(TSupplier defaultFunc)
        where TSupplier : struct, ISupplier<T>
        => IsSuccessful ? value : defaultFunc.Invoke();

    /// <summary>
    /// Returns the value if present; otherwise invoke delegate.
    /// </summary>
    /// <param name="defaultFunc">A delegate to be invoked if value is not present.</param>
    /// <returns>The value, if present, otherwise returned from delegate.</returns>
    public T OrInvoke(Func<T> defaultFunc)
        => OrInvoke<DelegatingSupplier<T>>(defaultFunc);

    /// <summary>
    /// Returns the value if present; otherwise invoke delegate.
    /// </summary>
    /// <param name="defaultFunc">A delegate to be invoked if value is not present.</param>
    /// <returns>The value, if present, otherwise returned from delegate.</returns>
    [CLSCompliant(false)]
    public unsafe T OrInvoke(delegate*<T> defaultFunc)
        => OrInvoke<Supplier<T>>(defaultFunc);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private T OrInvokeWithError<TSupplier>(TSupplier defaultFunc)
        where TSupplier : struct, ISupplier<TError, T>
        => IsSuccessful ? value : defaultFunc.Invoke(Error);

    /// <summary>
    /// Returns the value if present; otherwise invoke delegate.
    /// </summary>
    /// <param name="defaultFunc">A delegate to be invoked if value is not present.</param>
    /// <returns>The value, if present, otherwise returned from delegate.</returns>
    public T OrInvoke(Func<TError, T> defaultFunc)
        => OrInvokeWithError<DelegatingSupplier<TError, T>>(defaultFunc);

    /// <summary>
    /// Returns the value if present; otherwise invoke delegate.
    /// </summary>
    /// <param name="defaultFunc">A delegate to be invoked if value is not present.</param>
    /// <returns>The value, if present, otherwise returned from delegate.</returns>
    [CLSCompliant(false)]
    public unsafe T OrInvoke(delegate*<TError, T> defaultFunc)
        => OrInvokeWithError<Supplier<TError, T>>(defaultFunc);

    /// <inheritdoc cref="IFunctional{TDelegate}.ToDelegate()"/>
    Func<object?> IFunctional<Func<object?>>.ToDelegate() => Func.Constant<object?>(IsSuccessful ? value : null);

    private T OrThrow<TExceptionFactory>(TExceptionFactory factory)
        where TExceptionFactory : struct, ISupplier<TError, Exception>
        => IsSuccessful ? value : Throw(factory);

    [DoesNotReturn]
    [StackTraceHidden]
    private T Throw<TExceptionFactory>(TExceptionFactory factory)
        where TExceptionFactory : struct, ISupplier<TError, Exception>
        => throw factory.Invoke(Error);

    /// <summary>
    /// Gets underlying value or throws an exception.
    /// </summary>
    /// <param name="exceptionFactory">The exception factory that accepts the error code.</param>
    /// <returns>The underlying value.</returns>
    /// <exception cref="Exception">The result is unsuccessful.</exception>
    public T OrThrow(Func<TError, Exception> exceptionFactory)
        => OrThrow<DelegatingSupplier<TError, Exception>>(exceptionFactory);

    /// <summary>
    /// Gets underlying value or throws an exception.
    /// </summary>
    /// <param name="exceptionFactory">The exception factory that accepts the error code.</param>
    /// <returns>The underlying value.</returns>
    /// <exception cref="Exception">The result is unsuccessful.</exception>
    [CLSCompliant(false)]
    public unsafe T OrThrow(delegate*<TError, Exception> exceptionFactory)
        => OrThrow<Supplier<TError, Exception>>(exceptionFactory);

    /// <summary>
    /// Converts this result into <see cref="Result{T}"/>.
    /// </summary>
    /// <returns>The converted result.</returns>
    public Result<T> ToResult() => IsSuccessful ? new(value) : new(new UndefinedResultException<TError>(Error));

    /// <summary>
    /// Returns textual representation of this object.
    /// </summary>
    /// <returns>The textual representation of this object.</returns>
    public override string? ToString() => IsSuccessful ? value?.ToString() : errorCode.ToString();

    /// <summary>
    /// Returns the value if present; otherwise return default value.
    /// </summary>
    /// <param name="result">The result to check.</param>
    /// <param name="defaultValue">The value to be returned if this result is unsuccessful.</param>
    /// <returns>The value, if present, otherwise <paramref name="defaultValue"/>.</returns>
    public static T? operator |(in Result<T, TError> result, T? defaultValue)
        => result.Or(defaultValue);

    /// <summary>
    /// Tries to return successful result.
    /// </summary>
    /// <param name="x">The first container.</param>
    /// <param name="y">The second container.</param>
    /// <returns>The first successful result.</returns>
    public static Result<T, TError> operator |(in Result<T, TError> x, in Result<T, TError> y)
        => x.IsSuccessful ? x : y;

    /// <summary>
    /// Converts the result into <see cref="Optional{T}"/>.
    /// </summary>
    /// <param name="result">The result to be converted.</param>
    /// <returns>Option monad representing value in this monad.</returns>
    public static implicit operator Optional<T>(in Result<T, TError> result) => result.TryGet();

    /// <summary>
    /// Converts the result into <see cref="Result{T}"/>.
    /// </summary>
    /// <param name="result">The result to be converted.</param>
    /// <returns>The converted result.</returns>
    public static implicit operator Result<T>(in Result<T, TError> result) => result.ToResult();

    /// <summary>
    /// Converts value into the result.
    /// </summary>
    /// <param name="result">The result to be converted.</param>
    /// <returns>The result representing <paramref name="result"/> value.</returns>
    public static implicit operator Result<T, TError>(T result) => new(result);

    /// <summary>
    /// Extracts actual result.
    /// </summary>
    /// <param name="result">The result object.</param>
    public static explicit operator T(in Result<T, TError> result) => result.Value;

    /// <summary>
    /// Indicates that both results are successful.
    /// </summary>
    /// <param name="left">The first result to check.</param>
    /// <param name="right">The second result to check.</param>
    /// <returns><see langword="true"/> if both results are successful; otherwise, <see langword="false"/>.</returns>
    public static bool operator &(in Result<T, TError> left, in Result<T, TError> right) => left.IsSuccessful && right.IsSuccessful;

    /// <summary>
    /// Indicates that the result is successful.
    /// </summary>
    /// <param name="result">The result to check.</param>
    /// <returns><see langword="true"/> if this result is successful; <see langword="false"/> if this result represents exception.</returns>
    public static bool operator true(in Result<T, TError> result) => result.IsSuccessful;

    /// <summary>
    /// Indicates that the result represents error.
    /// </summary>
    /// <param name="result">The result to check.</param>
    /// <returns><see langword="false"/> if this result is successful; <see langword="true"/> if this result represents exception.</returns>
    public static bool operator false(in Result<T, TError> result) => !result.IsSuccessful;
}