using System.Net;
using Microsoft.AspNetCore.Connections;

namespace DotNext.Net;

using Patterns;

/// <summary>
/// Extends <see cref="UriEndPoint"/> type.
/// </summary>
public static class UriEndPointExtensions
{
    /// <summary>
    /// Extends <see cref="UriEndPoint"/> type.
    /// </summary>
    extension(UriEndPoint)
    {
        /// <summary>
        /// Gets comparer for <see cref="UriEndPoint"/> type.
        /// </summary>
        public static IEqualityComparer<EndPoint> Comparer => UriEndPointComparer.Instance;
    }
}

file sealed class UriEndPointComparer : IEqualityComparer<EndPoint>, ISingleton<UriEndPointComparer>
{
    public static UriEndPointComparer Instance { get; } = new();

    private UriEndPointComparer()
    {
    }

    /// <inheritdoc />
    bool IEqualityComparer<EndPoint>.Equals(EndPoint? x, EndPoint? y)
        => Equals((x as UriEndPoint)?.Uri, (y as UriEndPoint)?.Uri);

    int IEqualityComparer<EndPoint>.GetHashCode(EndPoint ep)
        => (ep as UriEndPoint)?.Uri.GetHashCode() ?? ep.GetHashCode();
}