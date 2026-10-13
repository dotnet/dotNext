using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Runtime.CompilerServices;

partial struct Atomic<T>
{
    /// <summary>
    /// Reads a field from the underlying value <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="TResult">The type of the field.</typeparam>
    /// <typeparam name="TReference">The field reference.</typeparam>
    /// <returns>The field value.</returns>
    public readonly TResult Read<TResult, TReference>()
        where TReference : IFieldReference<T, TResult>, allows ref struct
    {
        var spinner = new SpinWait();
        Read<TResult, FieldReadOperation<TResult, TReference>>(ref spinner, out var result);
        return result;
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct FieldReadOperation<TResult, TReader> : IReadOperation<TResult>
        where TReader : IFieldReference<T, TResult>, allows ref struct
    {
        static void IReadOperation<TResult>.Invoke(in T input, out TResult output)
            => output = TReader.GetReference(in input);
    }
}