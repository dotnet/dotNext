using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.InteropServices;

using Threading;

/// <summary>
/// Exposes <see cref="CancellationToken"/> to the unmanaged code.
/// </summary>
/// <seealso cref="UnmanagedCallersOnlyAttribute"/>
[StructLayout(LayoutKind.Sequential)]
public struct CancellationTokenHandle : IDisposable
{
    private GCHandle handle;

    /// <summary>
    /// Initializes a new handle for the specified cancellation token.
    /// </summary>
    /// <param name="token">The token to wrap.</param>
    public CancellationTokenHandle(CancellationToken token)
    {
        if (token.CanBeCanceled)
        {
            var reference = LinkedCancellationTokenSource.CanInlineToken
                ? Unsafe.BitCast<CancellationToken, ValueTuple<object>>(token).Item1
                : token;

            handle = GCHandle.Alloc(reference, GCHandleType.Normal);
        }
        else
        {
            handle = default;
        }
    }

    /// <summary>
    /// Unwraps the token.
    /// </summary>
    public readonly CancellationToken Token
    {
        get
        {
            if (!handle.IsAllocated || handle.Target is not { } reference)
                return CancellationToken.None;

            return LinkedCancellationTokenSource.CanInlineToken
                ? Unsafe.BitCast<ValueTuple<object>, CancellationToken>(new(reference))
                : Unsafe.Unbox<CancellationToken>(reference);
        }
    }

    /// <summary>
    /// Releases memory associated with the handle.
    /// </summary>
    public void Dispose()
    {
        if (handle.IsAllocated)
            handle.Free();
    }
}