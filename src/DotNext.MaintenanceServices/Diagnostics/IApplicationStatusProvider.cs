namespace DotNext.Diagnostics;

/// <summary>
/// Represents probes for the application running inside of Kubernetes.
/// </summary>
/// <seealso href="https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes">Kubernetes Probes</seealso>
public interface IApplicationStatusProvider
{
    /// <summary>
    /// Implements Readiness probe.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if probe is successfull; otherwise, <see langword="false"/>.</returns>
    Task<bool> ReadinessProbeAsync(CancellationToken token) => Task.FromResult(true);

    /// <summary>
    /// Implements Liveness probe.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if probe is successfull; otherwise, <see langword="false"/>.</returns>
    Task<bool> LivenessProbeAsync(CancellationToken token);

    /// <summary>
    /// Implements Startup probe.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if probe is successfull; otherwise, <see langword="false"/>.</returns>
    Task<bool> StartupProbeAsync(CancellationToken token) => Task.FromResult(true);
}