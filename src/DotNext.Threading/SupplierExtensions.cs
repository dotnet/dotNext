using System.Runtime.CompilerServices;

namespace DotNext;

using Timeout = Threading.Timeout;

internal static class SupplierExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TResult Invoke<TResult>(this ISupplier<TimeSpan, CancellationToken, TResult> supplier, CancellationToken token)
        => supplier.Invoke(new(Timeout.InfiniteTicks), token);
}