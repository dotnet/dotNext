using static System.Threading.Timeout;

namespace DotNext;

internal static class SupplierExtensions
{
    public static TResult Invoke<TResult>(this ISupplier<TimeSpan, CancellationToken, TResult> supplier, CancellationToken token)
        => supplier.Invoke(InfiniteTimeSpan, token);
}