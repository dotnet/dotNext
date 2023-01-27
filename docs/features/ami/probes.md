Kubernetes Probes
====
[Kubernetes probe](https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes) is another opt-in feature of AMI. The application may implement [IApplicationStatusProvider](xref:DotNext.Diagnostics.IApplicationStatusProvider) interface and register implementation in AMI host.

```csharp
using DotNext.Diagnostics;
using DotNext.Maintenance.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public sealed class Probes : IApplicationStatusProvider
{
    Task<bool> ReadinessProbeAsync(CancellationToken token) => Task.FromResult(true);

    Task<bool> LivenessProbeAsync(CancellationToken token) => Task.FromResult(true);

    Task<bool> StartupProbeAsync(CancellationToken token) => Task.FromResult(true);
}

await new HostBuilder()
    .ConfigureServices(services =>
    {
        services
            .UseApplicationMaintenanceInterface("/path/to/unix/domain/socket")
            .UseApplicationStatusProvider<Probes>();
    })
    .Build()
    .RunAsync();
```

After that, the probe can be described in YAML very simply:
```yaml
apiVersion: v1
kind: Pod
metadata:
  name: goproxy
  labels:
    app: goproxy
spec:
  containers:
  - name: goproxy
    image: k8s.gcr.io/goproxy:0.1
    ports:
    - containerPort: 8080
    readinessProbe:
      exec:
        command: sh /tmp/readiness_probe.sh
      initialDelaySeconds: 5
      periodSeconds: 5
```
```sh
# readiness_probe.sh file
PROBE=`echo probe readiness 00:00:05 | nc -U /tmp/app.sock`
if [ $? -eq 0 ] && [ $PROBE = "ok" ]
then
exit 0
else
exit 1
fi
```

The format of _probe_ command has the following format:
```
probe <readiness|liveness|startup> <timeout>
```
Timeout must be presented in [ISO-8601](https://en.wikipedia.org/wiki/ISO_8601#Durations) format for time durations.