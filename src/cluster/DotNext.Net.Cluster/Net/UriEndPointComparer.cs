using System.Net;
using Microsoft.AspNetCore.Connections;

namespace DotNext.Net;

internal sealed class UriEndPointComparer : IEqualityComparer<EndPoint>
{
    /// <inheritdoc />
    bool IEqualityComparer<EndPoint>.Equals(EndPoint? x, EndPoint? y)
        => Equals((x as UriEndPoint)?.Uri, (y as UriEndPoint)?.Uri);

    int IEqualityComparer<EndPoint>.GetHashCode(EndPoint ep)
        => ep is UriEndPoint uri ? uri.Uri.GetHashCode() : ep.GetHashCode();
}