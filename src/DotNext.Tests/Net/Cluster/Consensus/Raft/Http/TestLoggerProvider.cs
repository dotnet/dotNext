using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

using Buffers;
using Diagnostics;

[ExcludeFromCodeCoverage]
internal sealed class TestLoggerProvider : Disposable, ILoggerProvider
{
    private readonly string prefix;

    internal TestLoggerProvider(string prefix) => this.prefix = prefix;

    public ILogger CreateLogger(string name) => new Logger(name, prefix);

    private sealed class Logger : ILogger
    {
        private readonly string prefix, name;

        internal Logger(string name, string prefix)
        {
            this.prefix = prefix;
            this.name = name;
        }

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => NullLogger.Instance.BeginScope<TState>(state);

        public bool IsEnabled(LogLevel logLevel) => Debugger.IsAttached && logLevel is not LogLevel.None;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            string message = formatter?.Invoke(state, exception);

            if (string.IsNullOrEmpty(message))
                return;

            var buffer = new BufferWriterSlim<char>(stackalloc char[128]);
            buffer.WriteString($"[{prefix}][{new Timestamp()}]{logLevel}: {message}");

            if (exception is not null)
            {
                buffer.WriteLine();
                buffer.WriteLine();
                buffer.Write(exception.ToString());
            }

            message = buffer.ToString();
            buffer.Dispose();

            Debug.WriteLine(message, name);
        }
    }
}

[ExcludeFromCodeCoverage]
internal static class TestLoggerProviderExtensions
{
    private static TestLoggerProvider CreateProvider(this string prefix, IServiceProvider services)
        => new(prefix);

    internal static ILoggingBuilder AddTestDebugLogger(this ILoggingBuilder builder, string prefix)
    {
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, TestLoggerProvider>(prefix.CreateProvider));
        return builder;
    }
}