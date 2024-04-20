using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks.Sources;

namespace DotNext.IO;

using Intrinsics = Runtime.Intrinsics;

public partial class FileWriter : IDynamicInterfaceCastable
{
    private readonly Action writeCallback, writeAndCopyCallback;
    private ReadOnlyMemory<byte> secondBuffer;
    private ManualResetValueTaskSourceCore<byte> source;
    private ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter awaiter;

    private ReadOnlyMemory<byte> GetBuffer(int index) => index switch
    {
        0 => WrittenBuffer,
        1 => secondBuffer,
        _ => ReadOnlyMemory<byte>.Empty,
    };

    private IEnumerator<ReadOnlyMemory<byte>> EnumerateBuffers()
    {
        yield return WrittenBuffer;
        yield return secondBuffer;
    }

    [DynamicInterfaceCastableImplementation]
    private interface IBufferList : IReadOnlyList<ReadOnlyMemory<byte>>
    {
        int IReadOnlyCollection<ReadOnlyMemory<byte>>.Count => 2;

        ReadOnlyMemory<byte> IReadOnlyList<ReadOnlyMemory<byte>>.this[int index]
            => Unsafe.As<FileWriter>(this).GetBuffer(index);

        IEnumerator<ReadOnlyMemory<byte>> IEnumerable<ReadOnlyMemory<byte>>.GetEnumerator()
            => Unsafe.As<FileWriter>(this).EnumerateBuffers();

        IEnumerator IEnumerable.GetEnumerator()
            => Unsafe.As<FileWriter>(this).EnumerateBuffers();
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
        var awaiter = this.awaiter;
        this.awaiter = default;

        var secondBuffer = this.secondBuffer;
        this.secondBuffer = default;

        try
        {
            awaiter.GetResult();

            fileOffset += secondBuffer.Length + bufferOffset;
            bufferOffset = 0;
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
        var awaiter = this.awaiter;
        this.awaiter = default;

        var secondBuffer = this.secondBuffer;
        this.secondBuffer = default;

        try
        {
            awaiter.GetResult();

            fileOffset += bufferOffset;
            secondBuffer.CopyTo(buffer.Memory);
            bufferOffset = secondBuffer.Length;
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

        return new((IValueTaskSource)this, source.Version);
    }

    [DynamicInterfaceCastableImplementation]
    private interface IProxyValueTaskSource : IValueTaskSource
    {
        ValueTaskSourceStatus IValueTaskSource.GetStatus(short token)
            => Unsafe.As<FileWriter>(this).source.GetStatus(token);

        void IValueTaskSource.GetResult(short token)
            => Unsafe.As<FileWriter>(this).GetAsyncResult(token);

        void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
            => Unsafe.As<FileWriter>(this).source.OnCompleted(continuation, state, token, flags);
    }

    [ExcludeFromCodeCoverage]
    bool IDynamicInterfaceCastable.IsInterfaceImplemented(RuntimeTypeHandle interfaceType, bool throwIfNotImplemented)
    {
        if (interfaceType.IsOneOf([Intrinsics.TypeOf<IReadOnlyList<ReadOnlyMemory<byte>>>(), Intrinsics.TypeOf<IValueTaskSource>()]))
            return true;

        return throwIfNotImplemented ? throw new InvalidCastException() : false;
    }

    [ExcludeFromCodeCoverage]
    RuntimeTypeHandle IDynamicInterfaceCastable.GetInterfaceImplementation(RuntimeTypeHandle interfaceType)
    {
        if (interfaceType.IsOneOf([Intrinsics.TypeOf<IReadOnlyList<ReadOnlyMemory<byte>>>(), Intrinsics.TypeOf<IReadOnlyCollection<ReadOnlyMemory<byte>>>()]))
            return Intrinsics.TypeOf<IBufferList>();

        if (interfaceType.Equals(Intrinsics.TypeOf<IValueTaskSource>()))
            return Intrinsics.TypeOf<IProxyValueTaskSource>();

        throw new InvalidCastException();
    }
}