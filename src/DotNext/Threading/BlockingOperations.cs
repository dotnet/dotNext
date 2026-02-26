using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Runtime;
using Runtime.CompilerServices;

/// <summary>
/// Extends <see cref="System.Threading.Lock"/> and <see cref="Thread"/> types with cancellation token support for synchronization.
/// </summary>
public static class BlockingOperations
{
    /// <summary>
    /// Tries to acquire an exclusive lock on the specified object with cancellation support.
    /// </summary>
    /// <param name="lock">The object on which to acquire the lock.</param>
    /// <param name="timeout">Time to wait for the lock.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <param name="throwOnCancellation">
    /// <see langword="true"/> to throw <see cref="OperationCanceledException"/> if <paramref name="token"/> is canceled during the lock acquisition;
    /// <see langword="false"/> to return <see langword="false"/>.
    /// </param>
    /// <returns><see langword="true"/> if the lock is acquired successfully; <see langword="false"/> if timeout occurred or <paramref name="token"/> canceled.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is invalid.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="token"/> interrupts lock acquisition and <paramref name="throwOnCancellation"/> is <see langword="true"/>.</exception>
    public static bool TryEnter(this System.Threading.Lock @lock, TimeSpan timeout, CancellationToken token, bool throwOnCancellation = false)
    {
        Timeout.Validate(timeout);

        return Wait(new LockWrapper(@lock), timeout, token, throwOnCancellation);
    }

    /// <summary>
    /// Blocks the calling thread until the thread terminates, or the specified time elapses, or the token turns into
    /// canceled state.
    /// </summary>
    /// <param name="thread">The thread instance.</param>
    /// <param name="timeout">The time to wait.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <param name="throwOnCancellation">
    /// <see langword="true"/> to throw <see cref="OperationCanceledException"/> if <paramref name="token"/> is canceled and the thread is not yet completed;
    /// <see langword="false"/> to return <see langword="false"/>.
    /// </param>
    /// <returns><see langword="true"/> if the thread stops successfully; <see langword="false"/> if timeout occurred or <paramref name="token"/> canceled.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is invalid.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="token"/> interrupts the blocking operation and <paramref name="throwOnCancellation"/> is <see langword="true"/>.</exception>
    public static bool Join(this Thread thread, TimeSpan timeout, CancellationToken token, bool throwOnCancellation = false)
    {
        Timeout.Validate(timeout);

        return Wait(new ThreadWrapper(thread), timeout, token, throwOnCancellation);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct ThreadWrapper(Thread thread) : ISupplier<TimeSpan, bool>
    {
        bool ISupplier<TimeSpan, bool>.Invoke(TimeSpan timeout) => @thread.Join(timeout);

        void IFunctional.DynamicInvoke(scoped ref readonly Variant args, int count, scoped Variant result)
            => throw new NotSupportedException();
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct LockWrapper(System.Threading.Lock @lock) : ISupplier<TimeSpan, bool>
    {
        bool ISupplier<TimeSpan, bool>.Invoke(TimeSpan timeout) => @lock.TryEnter(timeout);

        void IFunctional.DynamicInvoke(scoped ref readonly Variant args, int count, scoped Variant result)
            => throw new NotSupportedException();
    }
    
    private static bool Wait<TLock>(TLock @lock, TimeSpan timeout, CancellationToken token, bool throwOnCancellation = false)
        where TLock : struct, ISupplier<TimeSpan, bool>, allows ref struct
    {
        var result = false;
        if (token.CanBeCanceled)
        {
            var registration = token.UnsafeRegister(Interrupt, Thread.CurrentThread);
            try
            {
                result = @lock.Invoke(timeout);
            }
            catch (ThreadInterruptedException e) when (token.IsCancellationRequested)
            {
                if (throwOnCancellation)
                    throw new OperationCanceledException(e.Message, e, token);

                goto exit;
            }
            finally
            {
                registration.Dispose();
            }

            // make sure that the interruption was not called on this thread concurrently with registration.Dispose()
            if (token.IsCancellationRequested)
            {
                try
                {
                    Thread.Sleep(0); // reset interrupted state
                }
                catch (ThreadInterruptedException)
                {
                    // suspend exception
                }
            }
        }
        else
        {
            result = @lock.Invoke(timeout);
        }

        exit:
        return result;
    }
    
    private static void Interrupt(object? thread)
    {
        Debug.Assert(thread is Thread);

        Unsafe.As<Thread>(thread).Interrupt();
    }
}