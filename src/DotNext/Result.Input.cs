using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace DotNext;

partial struct Result<T>
{
    /// <summary>
    /// Converts the monad to <see cref="Result{T}"/>.
    /// </summary>
    /// <param name="value">The value of the monad.</param>
    /// <typeparam name="TMonad">The type of the monad.</typeparam>
    /// <returns>The result constructed from the monad.</returns>
    public static Result<T> Create<TMonad>(TMonad value)
        where TMonad : struct, IResultMonad<T, Exception>
        => value switch
        {
            Result<T> => Unsafe.BitCast<TMonad, Result<T>>(value),
            Ok => Unsafe.BitCast<TMonad, Ok>(value),
            Failure => Unsafe.BitCast<TMonad, Failure>(value),
            _ => value.HasValue ? new(value.ValueOrDefault!) : new(value.Error!)
        };
    
    /// <summary>
    /// Represents successful result.
    /// </summary>
    /// <param name="value">The result value.</param>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Ok(T value) : IResultMonad<T>
    {
        /// <summary>
        /// Gets the underlying value.
        /// </summary>
        public T Value => value;
        
        /// <inheritdoc/>
        bool IOptionMonad<T>.HasValue => true;

        /// <inheritdoc/>
        T IOptionMonad<T>.ValueOrDefault => value;

        /// <inheritdoc/>
        Exception? IResultMonad<T, Exception>.Error => null;

        /// <inheritdoc/>
        T IResultMonad<T, Exception>.OrInvoke(Func<Exception, T> defaultFunc) => value;

        /// <summary>
        /// Converts successful result to <seealso cref="Result{T}"/> type.
        /// </summary>
        /// <param name="result">The result to convert.</param>
        /// <returns>An instance of <seealso cref="Result{T}"/> that represents the successful result.</returns>
        public static implicit operator Result<T>(Ok result) => new(result.Value);

        /// <summary>
        /// Converts the value to the monad.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The monad that represents the successful result.</returns>
        public static implicit operator Ok(T value) => new(value);
    }

    /// <summary>
    /// Represents unsuccessful result.
    /// </summary>
    /// <param name="error">The exception that represents the error.</param>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Failure(Exception error) : IResultMonad<T>
    {
        private readonly ExceptionDispatchInfo exception = ExceptionDispatchInfo.Capture(error);

        /// <summary>
        /// Gets the underlying exception.
        /// </summary>
        public Exception Error => exception.SourceException;

        /// <inheritdoc/>
        T IResultMonad<T>.Value
        {
            get
            {
                exception.Throw();
                return default;
            }
        }

        /// <inheritdoc/>
        bool IOptionMonad<T>.HasValue => false;

        /// <inheritdoc/>
        T? IOptionMonad<T>.ValueOrDefault => default;

        /// <inheritdoc/>
        T IResultMonad<T, Exception>.OrInvoke(Func<Exception, T> defaultFunc) => defaultFunc(Error);

        /// <summary>
        /// Converts successful result to <seealso cref="Result{T}"/> type.
        /// </summary>
        /// <param name="result">The result to convert.</param>
        /// <returns>An instance of <seealso cref="Result{T}"/> that represents the successful result.</returns>
        public static implicit operator Result<T>(Failure result) => new(result.exception);
        
        /// <summary>
        /// Converts the exception to the monad.
        /// </summary>
        /// <param name="error">The value to convert.</param>
        /// <returns>The monad that represents the failure.</returns>
        public static implicit operator Failure(Exception error) => new(error);
    }
}