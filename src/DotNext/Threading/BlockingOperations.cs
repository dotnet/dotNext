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
    /// <remarks>
    /// The method uses <see cref="Thread.Interrupt"/> to unblock the calling thread on cancellation.
    /// If the calling thread is interrupted by another party concurrently with cancellation,
    /// that interruption can be consumed by the method. If <paramref name="token"/> is already canceled,
    /// the method makes a single non-blocking attempt.
    /// </remarks>
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
    /// <remarks>
    /// The method uses <see cref="Thread.Interrupt"/> to unblock the calling thread on cancellation.
    /// If the calling thread is interrupted by another party concurrently with cancellation,
    /// that interruption can be consumed by the method. If <paramref name="token"/> is already canceled,
    /// the method makes a single non-blocking attempt.
    /// </remarks>
    public static bool Join(this Thread thread, TimeSpan timeout, CancellationToken token, bool throwOnCancellation = false)
    {
        Timeout.Validate(timeout);

        return Wait(new ThreadWrapper(thread), timeout, token, throwOnCancellation);
    }

    private static bool Wait<TLock>(TLock @lock, TimeSpan timeout, CancellationToken token, bool throwOnCancellation = false)
        where TLock : struct, ISupplier<TimeSpan, bool>, allows ref struct
    {
        if (!token.CanBeCanceled)
            return @lock.Invoke(timeout);

        bool result;

        // completion takes precedence over cancellation, but the thread must not interrupt itself
        if (token.IsCancellationRequested)
        {
            result = @lock.Invoke(TimeSpan.Zero);
        }
        else
        {
            // The callback is called at most once, so the thread receives at most one interruption from the token.
            // It can be observed by any interruptible call, including the spin-waits inside CancellationTokenSource.
            var interrupted = false;
            var registration = default(CancellationTokenRegistration);
            try
            {
                registration = token.UnsafeRegister(Interrupt, Thread.CurrentThread);
                result = @lock.Invoke(timeout);
            }
            catch (ThreadInterruptedException) when (token.IsCancellationRequested)
            {
                result = false;
                interrupted = true;
            }
            finally
            {
                // if the callback was called, the interruption is either consumed or pending
                if (registration.Unregister(ref interrupted) && !interrupted)
                    ResetInterruption();
            }
        }

        return result || !throwOnCancellation || !token.IsCancellationRequested
            ? result
            : throw new OperationCanceledException(token);

        static void ResetInterruption()
        {
            try
            {
                Thread.Sleep(0);
            }
            catch (ThreadInterruptedException)
            {
                // suspend exception
            }
        }

        static void Interrupt(object? thread)
        {
            Debug.Assert(thread is Thread);

            Unsafe.As<Thread>(thread).Interrupt();
        }
    }

    // returns true if the callback was called
    private static bool Unregister(this in CancellationTokenRegistration registration, ref bool interrupted)
    {
        while (true)
        {
            try
            {
                if (registration.Unregister())
                    return false;

                // wait for the callback to complete
                registration.Dispose();
                return true;
            }
            catch (ThreadInterruptedException) when (registration.Token.IsCancellationRequested)
            {
                interrupted = true;
            }
        }
    }
}

[StructLayout(LayoutKind.Auto)]
file readonly ref struct ThreadWrapper(Thread thread) : ISupplier<TimeSpan, bool>
{
    bool ISupplier<TimeSpan, bool>.Invoke(TimeSpan timeout) => thread.Join(timeout);

    void IFunctional.DynamicInvoke(scoped ref readonly Variant args, int count, scoped Variant result)
        => throw new NotSupportedException();
}

[StructLayout(LayoutKind.Auto)]
file readonly ref struct LockWrapper(System.Threading.Lock @lock) : ISupplier<TimeSpan, bool>
{
    bool ISupplier<TimeSpan, bool>.Invoke(TimeSpan timeout) => @lock.TryEnter(timeout);

    void IFunctional.DynamicInvoke(scoped ref readonly Variant args, int count, scoped Variant result)
        => throw new NotSupportedException();
}