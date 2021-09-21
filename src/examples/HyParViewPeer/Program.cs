using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using DotNext.Net.Cluster.Discovery.HyParView.Http;
using Microsoft.Extensions.Logging.Console;
using SslOptions = DotNext.Net.Security.SslOptions;

int port;
int? contactNodePort = null;

switch (args.Length)
{
    default:
        Console.WriteLine("Port number is not specified");
        return;
    case 1:
        port = int.Parse(args[0]);
        break;
    case 2:
        port = int.Parse(args[0]);
        contactNodePort = int.Parse(args[1]);
        break;
}

Console.WriteLine("Starting node...");

var configuration = new Dictionary<string, string>
{
    {"lowerShufflePeriod", "1000"},
    {"upperShufflePeriod", "5000"},
    {"activeViewCapacity", "3"},
    {"passiveViewCapacity", "6"},
    {"requestTimeout", "00:00:30"},
    {"localNode", $"https://localhost:{port}/"}
};

if (contactNodePort.HasValue)
    configuration.Add("contactNode", $"https://localhost:{contactNodePort.GetValueOrDefault()}");

await new HostBuilder().ConfigureWebHost(webHost =>
{
    webHost.UseKestrel(options =>
    {
        options.ListenLocalhost(port, static listener => listener.UseHttps(LoadCertificate()));
    })
    .UseStartup<HyParViewPeer.Startup>();
})
.ConfigureLogging(static builder => builder.AddConsole().SetMinimumLevel(LogLevel.Error))
.ConfigureAppConfiguration(builder => builder.AddInMemoryCollection(configuration))
.JoinMesh()
.Build()
.RunAsync();

static X509Certificate2 LoadCertificate()
{
    using var rawCertificate = Assembly.GetCallingAssembly().GetManifestResourceStream(typeof(Program), "node.pfx");
    using var ms = new MemoryStream(1024);
    rawCertificate?.CopyTo(ms);
    ms.Seek(0, SeekOrigin.Begin);
    return new X509Certificate2(ms.ToArray(), "1234");
}