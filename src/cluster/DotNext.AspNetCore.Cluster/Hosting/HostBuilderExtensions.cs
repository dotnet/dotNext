using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DotNext.Hosting
{
    /// <summary>
    /// Provides extension methods for <see cref="IHostBuilder"/> type.
    /// </summary>
    [CLSCompliant(false)]
    public static class HostBuilderExtensions
    {
        private static void ApplyOptions(this HostOptions options, IServiceCollection services)
            => services.AddSingleton<IOptions<HostOptions>>(Options.Create(options));

        /// <summary>
        /// Applies host options.
        /// </summary>
        /// <param name="builder">The host builder.</param>
        /// <param name="options">The host options.</param>
        /// <returns>The modified host builder.</returns>
        public static IHostBuilder UseHostOptions(this IHostBuilder builder, HostOptions options)
            => builder.ConfigureServices(options.ApplyOptions);
    }
}