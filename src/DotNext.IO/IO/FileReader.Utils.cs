using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks.Sources;

namespace DotNext.IO;

using Intrinsics = Runtime.Intrinsics;

public partial class FileReader : IDynamicInterfaceCastable
{
    private Action? readCallback, readDirectCallback;
    private ManualResetValueTaskSourceCore<int> source;
    private ConfiguredValueTaskAwaitable<int>.ConfiguredValueTaskAwaiter awaiter;
    private int extraCount;

    private Action ReadCallback => readCallback ??= OnRead;
    
    private Action ReadDirectCallback => readDirectCallback ??= OnReadDirect;

    private int GetAsyncResult(short token)
    {
        try
        {
            return source.GetResult(token);
        }
        finally
        {
            source.Reset();
        }
    }

    private void OnRead()
    {
        var awaiter = this.awaiter;
        this.awaiter = default;

        int count;
        try
        {
            count = awaiter.GetResult();

            bufferEnd += count;
        }
        catch (Exception e)
        {
            source.SetException(e);
            return;
        }

        source.SetResult(count);
    }

    private void OnReadDirect()
    {
        var awaiter = this.awaiter;
        this.awaiter = default;

        var extraCount = this.extraCount;
        this.extraCount = 0;

        int count;
        try
        {
            count = awaiter.GetResult();

            fileOffset += count;
            count += extraCount;
        }
        catch (Exception e)
        {
            source.SetException(e);
            return;
        }

        source.SetResult(count);
    }

    private ValueTask<int> SubmitAsInt32(ValueTask<int> task, Action callback)
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

        return new((IValueTaskSource<int>)this, source.Version);
    }

    private ValueTask<bool> SubmitAsBoolean(ValueTask<int> task, Action callback)
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

        return new((IValueTaskSource<bool>)this, source.Version);
    } 

    [DynamicInterfaceCastableImplementation]
    private interface IProxyValueTaskSource : IValueTaskSource<int>, IValueTaskSource<bool>
    {
        ValueTaskSourceStatus IValueTaskSource<int>.GetStatus(short token)
            => Unsafe.As<FileReader>(this).source.GetStatus(token);

        ValueTaskSourceStatus IValueTaskSource<bool>.GetStatus(short token)
            => Unsafe.As<FileReader>(this).source.GetStatus(token);

        int IValueTaskSource<int>.GetResult(short token)
            => Unsafe.As<FileReader>(this).GetAsyncResult(token);

        bool IValueTaskSource<bool>.GetResult(short token)
            => Unsafe.As<FileReader>(this).GetAsyncResult(token) is not 0;

        void IValueTaskSource<int>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
            => Unsafe.As<FileReader>(this).source.OnCompleted(continuation, state, token, flags);

        void IValueTaskSource<bool>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
            => Unsafe.As<FileReader>(this).source.OnCompleted(continuation, state, token, flags);
    }

    [ExcludeFromCodeCoverage]
    bool IDynamicInterfaceCastable.IsInterfaceImplemented(RuntimeTypeHandle interfaceType, bool throwIfNotImplemented)
    {
        if (interfaceType.IsOneOf([Intrinsics.TypeOf<IValueTaskSource<int>>(), Intrinsics.TypeOf<IValueTaskSource<bool>>()]))
            return true;

        return throwIfNotImplemented ? throw new InvalidCastException() : false;
    }

    [ExcludeFromCodeCoverage]
    RuntimeTypeHandle IDynamicInterfaceCastable.GetInterfaceImplementation(RuntimeTypeHandle interfaceType)
    {
        if (interfaceType.IsOneOf([Intrinsics.TypeOf<IValueTaskSource<int>>(), Intrinsics.TypeOf<IValueTaskSource<bool>>()]))
            return Intrinsics.TypeOf<IProxyValueTaskSource>();

        throw new InvalidCastException();
    }
}