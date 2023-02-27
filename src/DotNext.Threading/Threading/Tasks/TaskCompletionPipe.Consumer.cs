using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Threading.Tasks;

using Intrinsics = Runtime.Intrinsics;

public partial class TaskCompletionPipe<T> : IDynamicInterfaceCastable
{
    internal static Tuple<RuntimeTypeHandle, RuntimeTypeHandle>? RuntimeEnumerableInfo;

    /// <inheritdoc />
    bool IDynamicInterfaceCastable.IsInterfaceImplemented(RuntimeTypeHandle interfaceType, bool throwIfNotImplemented)
    {
        var info = RuntimeEnumerableInfo;
        return info is not null && interfaceType.Equals(info.Item1) || (throwIfNotImplemented ? throw new InvalidCastException() : false);
    }

    /// <inheritdoc />
    RuntimeTypeHandle IDynamicInterfaceCastable.GetInterfaceImplementation(RuntimeTypeHandle interfaceType)
    {
        var info = RuntimeEnumerableInfo;
        return info is not null && interfaceType.Equals(info.Item1) ? info.Item2 : default;
    }
}

/// <summary>
/// Provides various extension methods for <see cref="TaskCompletionPipe{T}"/> class.
/// </summary>
public static class TaskCompletionPipe
{
    [DynamicInterfaceCastableImplementation]
    private interface ITypedTaskCompletionPipe<T> : IAsyncEnumerable<T>
    {
        private static async IAsyncEnumerator<T> GetAsyncEnumerator(TaskCompletionPipe<Task<T>> pipe, CancellationToken token)
        {
            while (await pipe.TryDequeue(out var task).Invoke(token).ConfigureAwait(false))
            {
                if (task is not null)
                {
                    Debug.Assert(task.IsCompleted);

                    yield return await task.ConfigureAwait(false);
                }
            }
        }

        IAsyncEnumerator<T> IAsyncEnumerable<T>.GetAsyncEnumerator(CancellationToken token)
        {
            Debug.Assert(this is TaskCompletionPipe<Task<T>>);

            return GetAsyncEnumerator(Unsafe.As<TaskCompletionPipe<Task<T>>>(this), token);
        }
    }

    /// <summary>
    /// Gets asynchronous consumer.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the consuming collection.</typeparam>
    /// <param name="pipe">The task completion pipe with typed tasks.</param>
    /// <returns>The asynchronous consuming collection.</returns>
    public static IAsyncEnumerable<T> GetConsumer<T>(this TaskCompletionPipe<Task<T>> pipe)
    {
        // dynamic interface dispatch must be compatible with AOT. Thus, we cannot use things like Type.MakeGenericType
        TaskCompletionPipe<Task<T>>.RuntimeEnumerableInfo ??= new(Intrinsics.TypeOf<IAsyncEnumerable<T>>(), Intrinsics.TypeOf<ITypedTaskCompletionPipe<T>>());
        return (IAsyncEnumerable<T>)pipe;
    }
}