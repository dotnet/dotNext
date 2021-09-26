using System.Net;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace DotNext.Net.Http;

internal static class HttpUtils
{
    internal static Task WriteExceptionContent(HttpContext context)
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        return feature is null ? Task.CompletedTask : context.Response.WriteAsync(feature.Error.ToString());
    }

    internal static void SetProtocolVersion(this HttpClient client, HttpProtocolVersion version)
    {
        switch (version)
        {
            case HttpProtocolVersion.Http1:
                client.DefaultRequestVersion = HttpVersion.Version11;
                break;
            case HttpProtocolVersion.Http2:
                client.DefaultRequestVersion = HttpVersion.Version20;
                break;
            case HttpProtocolVersion.Http3:
                client.DefaultRequestVersion = HttpVersion.Version30;
                break;
        }
    }
}