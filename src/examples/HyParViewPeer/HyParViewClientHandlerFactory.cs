using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace HyParViewPeer;

internal sealed class HyParViewClientHandlerFactory : IHttpMessageHandlerFactory
{
    private static bool AllowCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors) => true;

    public HttpMessageHandler CreateHandler(string name)
    {
        var handler = new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromMilliseconds(1000) };
        handler.SslOptions.RemoteCertificateValidationCallback = AllowCertificate;
        return handler;
    }
}