using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext;

/// <summary>
/// Extends <see cref="ArgumentException"/> type.
/// </summary>
public static class ArgumentExceptionExtensions
{
    /// <summary>
    /// Adds additional static checkers to <see cref="ArgumentException"/> type.
    /// </summary>
    extension(ArgumentException)
    {
        /// <summary>
        /// Throws if the input buffer is empty.
        /// </summary>
        /// <param name="span">The buffer argument to validate.</param>
        /// <param name="paramName">The name of the parameter with which argument corresponds.</param>
        /// <typeparam name="T">The type of the elements in the buffer.</typeparam>
        public static void ThrowIfEmpty<T>(ReadOnlySpan<T> span, [CallerArgumentExpression(nameof(span))] string? paramName = null)
        {
            if (span.IsEmpty)
                ThrowBufferTooSmall(paramName);
        }
        
        /// <summary>
        /// Throws if the input buffer is empty.
        /// </summary>
        /// <param name="span">The buffer argument to validate.</param>
        /// <param name="paramName">The name of the parameter with which argument corresponds.</param>
        /// <typeparam name="T">The type of the elements in the buffer.</typeparam>
        public static void ThrowIfEmpty<T>(Span<T> span, [CallerArgumentExpression(nameof(span))] string? paramName = null)
        {
            if (span.IsEmpty)
                ThrowBufferTooSmall(paramName);
        }
        
        /// <summary>
        /// Throws if the input buffer is empty.
        /// </summary>
        /// <param name="memory">The buffer argument to validate.</param>
        /// <param name="paramName">The name of the parameter with which argument corresponds.</param>
        /// <typeparam name="T">The type of the elements in the buffer.</typeparam>
        public static void ThrowIfEmpty<T>(in ReadOnlyMemory<T> memory, [CallerArgumentExpression(nameof(memory))] string? paramName = null)
        {
            if (memory.IsEmpty)
                ThrowBufferTooSmall(paramName);
        }
        
        /// <summary>
        /// Throws if the input buffer is empty.
        /// </summary>
        /// <param name="memory">The buffer argument to validate.</param>
        /// <param name="paramName">The name of the parameter with which argument corresponds.</param>
        /// <typeparam name="T">The type of the elements in the buffer.</typeparam>
        public static void ThrowIfEmpty<T>(in Memory<T> memory, [CallerArgumentExpression(nameof(memory))] string? paramName = null)
        {
            if (memory.IsEmpty)
                ThrowBufferTooSmall(paramName);
        }

        /// <summary>
        /// Throws if the length of <paramref name="memory"/> is less than the desired value.
        /// </summary>
        /// <param name="memory">The memory to validate.</param>
        /// <param name="desiredLength">The desired length.</param>
        /// <param name="paramName">The name of the parameter with which argument corresponds.</param>
        /// <typeparam name="T">The type of the elements in the buffer.</typeparam>
        public static void ThrowIfShorterThan<T>(in Memory<T> memory, int desiredLength, [CallerArgumentExpression(nameof(memory))] string? paramName = null)
        {
            if (memory.Length < desiredLength)
                ThrowBufferTooSmall(paramName);
        }

        /// <summary>
        /// Creates <see cref="ArgumentException"/> that describes the empty buffer.
        /// </summary>
        /// <param name="paramName">The name of the parameter.</param>
        /// <returns></returns>
        public static ArgumentException BufferTooSmall(string? paramName)
            => new(ExceptionMessages.BufferTooSmall, paramName);

        [DoesNotReturn]
        [StackTraceHidden]
        private static void ThrowBufferTooSmall(string? paramName)
            => throw BufferTooSmall(paramName);
    }
}