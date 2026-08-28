using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks.Sources;

namespace DotNext.IO;

using Buffers;
using Runtime.CompilerServices;

public partial class FileBufferingWriter : IDynamicInterfaceCastable
{
    private readonly Action writeCallback, writeAndFlushCallback, writeAndCopyCallback;
    private ReadOnlyMemory<byte> secondBuffer;
    private ManualResetValueTaskSourceCore<byte> source;
    private ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter awaiter;

    private ReadOnlyMemory<byte> GetBuffer(int index) => index switch
    {
        0 => WrittenMemory,
        1 => secondBuffer,
        _ => ReadOnlyMemory<byte>.Empty,
    };

    private IEnumerator<ReadOnlyMemory<byte>> EnumerateBuffers()
    {
        yield return WrittenMemory;
        yield return secondBuffer;
    }

    [DynamicInterfaceCastableImplementation]
    private interface IBufferList : IReadOnlyList<ReadOnlyMemory<byte>>
    {
        int IReadOnlyCollection<ReadOnlyMemory<byte>>.Count => 2;

        ReadOnlyMemory<byte> IReadOnlyList<ReadOnlyMemory<byte>>.this[int index]
            => Unsafe.As<FileBufferingWriter>(this).GetBuffer(index);

        IEnumerator<ReadOnlyMemory<byte>> IEnumerable<ReadOnlyMemory<byte>>.GetEnumerator()
            => Unsafe.As<FileBufferingWriter>(this).EnumerateBuffers();

        IEnumerator IEnumerable.GetEnumerator()
            => Unsafe.As<FileBufferingWriter>(this).EnumerateBuffers();
    }

    private void GetAsyncResult(short token)
    {
        try
        {
            source.GetResult(token);
        }
        finally
        {
            source.Reset();
        }
    }

    private void OnWrite()
    {
        var awaiterCopy = awaiter;
        awaiter = default;

        var secondBufferCopy = this.secondBuffer;
        secondBuffer = default;

        try
        {
            awaiterCopy.GetResult();

            filePosition += secondBufferCopy.Length + position;
            position = 0;
        }
        catch (Exception e)
        {
            source.SetException(e);
            return;
        }

        source.SetResult(0);
    }

    private void OnWriteAndFlush()
    {
        Debug.Assert(fileBackend is not null);

        var awaiterCopy = awaiter;
        awaiter = default;

        var secondBufferCopy = secondBuffer;
        secondBuffer = default;

        try
        {
            awaiterCopy.GetResult();

            filePosition += secondBufferCopy.Length + position;
            position = 0;
            RandomAccess.FlushToDisk(fileBackend);
        }
        catch (Exception e)
        {
            source.SetException(e);
            return;
        }

        source.SetResult(0);
    }

    private void OnWriteAndCopy()
    {
        var awaiterCopy = awaiter;
        awaiter = default;

        var secondBufferCopy = this.secondBuffer;
        secondBuffer = default;

        try
        {
            awaiterCopy.GetResult();

            filePosition += position;
            buffer.Resize(secondBufferCopy.Length, allocator);
            secondBufferCopy.CopyTo(buffer.Memory);
            position = secondBufferCopy.Length;
        }
        catch (Exception e)
        {
            source.SetException(e);
            return;
        }

        source.SetResult(0);
    }

    private ValueTask Submit(ValueTask task, Action callback)
    {
        awaiter = task.ConfigureAwait(false).GetAwaiter();
        if (awaiter.IsCompleted)
        {
            callback();
        }
        else
        {
            awaiter.UnsafeOnCompleted(callback);
        }

        return new((IValueTaskSource)this.As<IDynamicInterfaceCastable>(), source.Version);
    }

    [DynamicInterfaceCastableImplementation]
    private interface IProxyValueTaskSource : IValueTaskSource
    {
        ValueTaskSourceStatus IValueTaskSource.GetStatus(short token)
        {
            ref var source = ref Unsafe.As<FileBufferingWriter>(this).source;
            return source.GetStatus(token);
        }

        void IValueTaskSource.GetResult(short token)
            => Unsafe.As<FileBufferingWriter>(this).GetAsyncResult(token);

        void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            ref var source = ref Unsafe.As<FileBufferingWriter>(this).source;
            source.OnCompleted(continuation, state, token, flags);
        }
    }

    [ExcludeFromCodeCoverage]
    bool IDynamicInterfaceCastable.IsInterfaceImplemented(RuntimeTypeHandle interfaceType, bool throwIfNotImplemented)
    {
        if (interfaceType.IsOneOf(IReadOnlyList<ReadOnlyMemory<byte>>.TypeId, IValueTaskSource.TypeId))
            return true;

        return throwIfNotImplemented ? throw new InvalidCastException() : false;
    }

    [ExcludeFromCodeCoverage]
    RuntimeTypeHandle IDynamicInterfaceCastable.GetInterfaceImplementation(RuntimeTypeHandle interfaceType)
    {
        if (interfaceType.IsOneOf(IReadOnlyList<ReadOnlyMemory<byte>>.TypeId,
                IReadOnlyCollection<ReadOnlyMemory<byte>>.TypeId))
            return IBufferList.TypeId;

        if (interfaceType.Equals(IValueTaskSource.TypeId))
            return IProxyValueTaskSource.TypeId;

        throw new InvalidCastException();
    }
}