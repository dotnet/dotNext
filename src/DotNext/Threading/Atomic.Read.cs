using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Diagnostics.CodeAnalysis;

partial struct Atomic<T>
{
    /// <summary>
    /// Reads a field from the underlying value <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="TResult">The type of the field.</typeparam>
    /// <typeparam name="TReference">The field reference.</typeparam>
    /// <returns>The field value.</returns>
    [MemoryOrdering(MemoryOrdering.Acquire)]
    public readonly TResult Read<TResult, TReference>()
        where TReference : IFieldReference<TResult>, allows ref struct
    {
        var spinner = new SpinWait();
        Read<TResult, FieldReadOperation<TResult, TReference>>(ref spinner, out var result);
        return result;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct FieldReadOperation<TResult, TReader> : IReadOperation<TResult>
        where TReader : IFieldReference<TResult>, allows ref struct
    {
        static void IReadOperation<TResult>.Invoke(in T input, out TResult output)
            => output = TReader.GetReference(in input);
    }
    
    /// <summary>
    /// Provides field location in the memory.
    /// </summary>
    /// <typeparam name="TResult">The type of the field.</typeparam>
    public interface IFieldReference<TResult>
        where TResult : allows ref struct
    {
        /// <summary>
        /// Obtains a reference to the field.
        /// </summary>
        /// <param name="value">The underlying value.</param>
        /// <returns>The field reference.</returns>
        public static abstract ref readonly TResult GetReference(in T value);
    }
}