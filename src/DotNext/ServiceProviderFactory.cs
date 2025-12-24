using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext;

using Patterns;

/// <summary>
/// Represents various ways for building <see cref="IServiceProvider"/> implementations.
/// </summary>
public static partial class ServiceProviderFactory
{
    /// <summary>
    /// Extends <see cref="IServiceProvider"/> type.
    /// </summary>
    /// <param name="provider">The service provider to extend.</param>
    extension(IServiceProvider provider)
    {
        /// <summary>
        /// Gets the empty service provider.
        /// </summary>
        public static IServiceProvider Empty => EmptyServiceProvider.Instance;

        /// <summary>
        /// Creates a builder of the custom <see cref="IServiceProvider"/> implementation.
        /// </summary>
        /// <returns>The builder instance.</returns>
        public static Builder CreateBuilder() => new();

        /// <summary>
        /// Creates service provider from the tuple.
        /// </summary>
        /// <param name="tuple">The tuple containing services.</param>
        /// <param name="fallback">The fallback provider used for service resolution if tuple doesn't contain the requested service.</param>
        /// <typeparam name="T">The tuple type representing a set of services.</typeparam>
        /// <returns>The service provider constructed from the tuple.</returns>
        public static IServiceProvider Create<T>(T tuple, IServiceProvider? fallback = null)
            where T : struct, ITuple
            => tuple.Length switch
            {
                0 => EmptyServiceProvider.Instance,
                1 => new SingleServiceProvider<ValueSupplier<object?>>(CachedServiceProvider<T>.GetServiceType(0), new(tuple[0]), fallback),
                _ => new CachedServiceProvider<T>(tuple, fallback),
            };

        /// <summary>
        /// Overrides the service that can be returned by the underlying service provider.
        /// </summary>
        /// <param name="service">The service instance.</param>
        /// <typeparam name="TService">The type of the service.</typeparam>
        /// <returns>A new service provider that returns <paramref name="service"/> if the requested type is <typeparamref name="TService"/>.</returns>
        public IServiceProvider Override<TService>(TService service)
            where TService : notnull
            => new SingleServiceProvider<ValueSupplier<object?>>(typeof(TService), new(service), provider);

        /// <summary>
        /// Overrides the service that can be returned by the underlying service provider.
        /// </summary>
        /// <param name="serviceGetter">The service instance getter.</param>
        /// <typeparam name="TService">The type of the service.</typeparam>
        /// <returns>A new service provider that returns the result of <paramref name="serviceGetter"/> invocation if the requested type is <typeparamref name="TService"/>.</returns>
        public IServiceProvider Override<TService>(Func<TService> serviceGetter)
            where TService : class
            => new SingleServiceProvider<DelegatingSupplier<object>>(typeof(TService), new(serviceGetter), provider);

        /// <summary>
        /// Overrides the service that can be returned by the underlying service provider.
        /// </summary>
        /// <param name="serviceGetter">The service instance getter.</param>
        /// <typeparam name="TService">The type of the service.</typeparam>
        /// <returns>A new service provider that returns the result of <paramref name="serviceGetter"/> invocation if the requested type is <typeparamref name="TService"/>.</returns>
        public IServiceProvider Override<TService>(ISupplier<TService> serviceGetter)
            where TService : class
            => new SingleServiceProvider<ISupplier<TService>>(typeof(TService), serviceGetter, provider);
    }

    private sealed class CachedServiceProvider<T>(in T tuple, IServiceProvider? fallback) : IServiceProvider
        where T : struct, ITuple
    {
        private static readonly ReadOnlyMemory<Type> ServiceTypes;

        static CachedServiceProvider()
        {
            var tupleType = typeof(T);
            ServiceTypes = tupleType.IsConstructedGenericType ? tupleType.GetGenericArguments() : [];
        }

        private T tuple = tuple;

        object? IServiceProvider.GetService(Type serviceType)
        {
            var index = ServiceTypes.Span.IndexOf(serviceType);
            return (uint)index < (uint)tuple.Length ? tuple[index] : fallback?.GetService(serviceType);
        }

        public static Type GetServiceType(int index) => ServiceTypes.Span[index];
    }

    private sealed class EmptyServiceProvider : IServiceProvider, ISingleton<EmptyServiceProvider>
    {
        public static EmptyServiceProvider Instance { get; } = new();

        private EmptyServiceProvider()
        {
        }

        object? IServiceProvider.GetService(Type serviceType) => null;
    }

    private sealed class SingleServiceProvider<TProvider>(Type expectedType, TProvider provider, IServiceProvider? fallback) : IServiceProvider
        where TProvider : ISupplier<object?>
    {
        private TProvider provider = provider;

        object? IServiceProvider.GetService(Type serviceType)
            => expectedType == serviceType ? provider.Invoke() : fallback?.GetService(serviceType);
    }
}