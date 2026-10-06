using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

/// <summary>
/// Represents consumer part of the resource which lifetime is managed by the reference counter in thread-safe manner.
/// </summary>
/// <remarks>
/// All instance methods are thread-safe.
/// </remarks>
/// <typeparam name="T">The type of the resource.</typeparam>
[DebuggerDisplay($"IsAlive = {{{nameof(IsAlive)}}}")]
public abstract class ReferenceCounted<T>
    where T : IDisposable
{
    private T resource;
    private nuint counter;

    private protected ReferenceCounted(T resource, [CallerArgumentExpression(nameof(resource))] string paramName = "")
    {
        this.resource = resource ?? throw new ArgumentNullException(paramName);
        counter = OwnerFlag;
    }
    
    private static nuint OwnerFlag => (nuint)nint.MinValue;

    private static nuint OwnerMask => ~OwnerFlag;

    [ExcludeFromCodeCoverage]
    private bool IsAlive => Volatile.Read(in counter) is not 0U;
    
    private nuint TryChange(nuint delta)
    {
        var current = counter;

        if (current > 0U && Interlocked.CompareExchange(ref counter, current + delta, current) != current)
        {
            current = Contention(ref counter, delta);
        }

        return current;
        
        static nuint Contention(ref nuint counter, nuint delta)
        {
            var current = counter;
            for (nuint tmp; current > 0U; current = tmp)
            {
                tmp = Interlocked.CompareExchange(ref counter, current + delta, current);
                if (tmp == current)
                    break;
            }

            return current;
        }
    }

    private bool TryAcquire() => TryChange(delta: 1U) > 0U;

    private void Release()
    {
        // MaxValue is equivalent to x - 1 operation in unsigned arithmetics
        if (TryChange(delta: nuint.MaxValue) is 1U)
        {
            resource.Dispose();
            resource = default!;
        }
    }

    private bool TryReleaseOwnership()
    {
        if ((counter & OwnerFlag) is not 0U)
        {
            var previous = nuint.Size is sizeof(ulong)
                ? (nuint)Interlocked.And(ref Unsafe.As<nuint, ulong>(ref counter), OwnerMask)
                : Interlocked.And(ref Unsafe.As<nuint, uint>(ref counter), (uint)OwnerMask);

            return previous == OwnerFlag;
        }

        return false;
    }

    private protected void ReleaseOwnership()
    {
        if (TryReleaseOwnership())
        {
            resource.Dispose();
            resource = default!;
        }
    }

    /// <summary>
    /// Acquires the access to the resource.
    /// </summary>
    /// <returns>
    /// The scope which guarantees the liveness of the resource.
    /// The caller must check <see cref="Scope.IsValid"/> property before
    /// accessing <see cref="Scope.Value"/>.
    /// </returns>
    public Scope Acquire() => new(this);

    /// <summary>
    /// Represents a reference that is protected by the counter increment.
    /// </summary>
    /// <remarks>
    /// Strong reference is a short-lived scope which protects the resource from being disposed.
    /// </remarks>
    [StructLayout(LayoutKind.Auto)]
    public struct Scope : IOptionMonad<T>, IDisposable
    {
        private ReferenceCounted<T>? arc;

        internal Scope(ReferenceCounted<T> arc)
            => this.arc = arc.TryAcquire() ? arc : null;
        
        /// <summary>
        /// Gets a value indicating that this reference is valid and can be used to access the resource.
        /// </summary>
        [MemberNotNullWhen(true, nameof(arc))]
        public readonly bool IsValid => arc is not null;

        /// <summary>
        /// Gets the protected resource.
        /// </summary>
        public readonly T Value
        {
            get
            {
                var containerCopy = arc;
                if (containerCopy is null)
                    InvalidOperationException.Throw();

                return containerCopy.resource;
            }
        }

        /// <summary>
        /// Gets a reference to the protected resource.
        /// </summary>
        /// <remarks>
        /// This property is useful when the underlying resource is a value type.
        /// </remarks>
        public readonly ref T ValueRef
        {
            get
            {
                var containerCopy = arc;
                if (containerCopy is null)
                    InvalidOperationException.Throw();

                return ref containerCopy.resource;
            }
        }

        /// <summary>
        /// Releases the strong reference.
        /// </summary>
        /// <remarks>
        /// This method must not be called more than once.
        /// </remarks>
        public void Dispose()
        {
            if (IsValid)
            {
                arc.Release();
                arc = null;
            }
        }

        /// <inheritdoc/>
        readonly bool IOptionMonad<T>.HasValue => IsValid;

        /// <inheritdoc/>
        readonly T? IOptionMonad<T>.ValueOrDefault => IsValid ? arc.resource : default;
    }
}

/// <summary>
/// Represents owner part of the resource which lifetime is managed by the reference counter in thread-safe manner.
/// </summary>
/// <param name="resource">The resource to be protected by the reference counting.</param>
/// <typeparam name="T">The type of the resource.</typeparam>
public sealed class ReferenceCountedOwner<T>(T resource) : ReferenceCounted<T>(resource), IDisposable
    where T : IDisposable
{
    /// <summary>
    /// Releases the ownership of the resource.
    /// </summary>
    public void Dispose() => ReleaseOwnership();
}

/// <summary>
/// Provides extensions for <see cref="ReferenceCounted{T}"/> class.
/// </summary>
public static class ReferenceCounted
{
    /// <summary>
    /// Acquires the resource.
    /// </summary>
    /// <param name="arc">The container.</param>
    /// <param name="resource">The acquired resource; or <see langword="null"/> if resource is reclaimed.</param>
    /// <typeparam name="T">The type of the resource.</typeparam>
    /// <returns>The scope which controls the lifetime of the reference.</returns>
    public static ReferenceCounted<T>.Scope Acquire<T>(this ReferenceCounted<T> arc, out T? resource)
        where T : class, IDisposable
    {
        var scope = new ReferenceCounted<T>.Scope(arc);
        resource = GetValueOrDefault(scope);
        return scope;

        static T? GetValueOrDefault<TMonad>(TMonad scope)
            where TMonad : struct, IOptionMonad<T>
            => scope.ValueOrDefault;
    }
}