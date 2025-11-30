using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace DotNext;

using Reflection;
using Patterns;

/// <summary>
/// Represents various ways for building <see cref="IServiceProvider"/> implementations.
/// </summary>
public static partial class ServiceProviderFactory
{
    private sealed class CompiledServiceProvider : IServiceProvider
    {
        private readonly Func<Type, IReadOnlyList<object?>, object?> resolver;
        private readonly IReadOnlyList<object?> services;

        internal CompiledServiceProvider(Func<Type, IReadOnlyList<object?>, object?> resolver, IReadOnlyList<object?> services)
        {
            this.services = services;
            this.resolver = resolver;
        }

        object? IServiceProvider.GetService(Type serviceType)
            => resolver(serviceType, services);
    }

    private sealed class DelegatingServiceProvider : IServiceProvider
    {
        private readonly Func<Type, IReadOnlyList<object>, IServiceProvider, object?> resolver;
        private readonly IReadOnlyList<object> services;
        private readonly IServiceProvider fallback;

        internal DelegatingServiceProvider(Func<Type, IReadOnlyList<object>, IServiceProvider, object?> resolver, IReadOnlyList<object> services, IServiceProvider fallback)
        {
            this.resolver = resolver;
            this.services = services;
            this.fallback = fallback;
        }

        object? IServiceProvider.GetService(Type serviceType)
            => resolver(serviceType, services, fallback);
    }

    private sealed class CachedServiceProvider : Dictionary<Type, object?>, IServiceProvider
    {
        private readonly IServiceProvider? fallback;

        internal CachedServiceProvider(IDictionary<Type, object?> services, IServiceProvider? fallback)
            : base(services)
            => this.fallback = fallback;

        internal CachedServiceProvider(IServiceProvider? fallback, params KeyValuePair<Type, object?>[] services)
            : base(services)
            => this.fallback = fallback;

        internal CachedServiceProvider(int capacity, IServiceProvider? fallback)
            : base(capacity)
            => this.fallback = fallback;

        object? IServiceProvider.GetService(Type serviceType)
            => TryGetValue(serviceType, out var service) ? service : fallback?.GetService(serviceType);
    }

    private sealed class CachedServiceProvider<T> : IServiceProvider
        where T : struct, ITuple
    {
        private static readonly Type[] ServiceTypes;

        static CachedServiceProvider()
        {
            var tupleType = typeof(T);
            ServiceTypes = tupleType.IsConstructedGenericType ? tupleType.GetGenericArguments() : [];
        }

        private readonly IServiceProvider? fallback;
        private T tuple;    // is not read-only to avoid defensive copies

        internal CachedServiceProvider(T tuple, IServiceProvider? fallback)
        {
            this.tuple = tuple;
            this.fallback = fallback;
        }

        object? IServiceProvider.GetService(Type serviceType)
        {
            var index = Array.IndexOf(ServiceTypes, serviceType);
            return (uint)index < (uint)tuple.Length ? tuple[index] : fallback?.GetService(serviceType);
        }
    }

    private sealed class EmptyProvider : IServiceProvider, ISingleton<EmptyProvider>
    {
        public static EmptyProvider Instance { get; } = new();

        private EmptyProvider()
        {
        }
        
        object? IServiceProvider.GetService(Type serviceType) => null;
    }

    /// <summary>
    /// Extends <see cref="IServiceProvider"/> type.
    /// </summary>
    extension(IServiceProvider)
    {
        /// <summary>
        /// Gets the empty service provider.
        /// </summary>
        public static IServiceProvider Empty => EmptyProvider.Instance;

        /// <summary>
        /// Creates a builder of the custom <see cref="IServiceProvider"/> implementation.
        /// </summary>
        /// <returns>The builder instance.</returns>
        public static Builder CreateBuilder() => new();

        /// <summary>
        /// Creates factory that can be used to construct the service provider.
        /// </summary>
        /// <remarks>
        /// The constructed factory compiles specialization of the service provider on-the-fly
        /// using dynamic code compilation technique when applicable. If dynamic code compilation
        /// is not supported then the factory uses the implementation backed by dictionary.
        /// </remarks>
        /// <param name="types">The array of supported types by the service provider.</param>
        /// <returns>The delegate that can be used to instantiate specialized service provider.</returns>
        public static Func<IReadOnlyList<object>, IServiceProvider> CreateFactory(params Type[] types)
            => RuntimeFeature.IsDynamicCodeCompiled ? CreateResolver(types).Create : types.Create;

        /// <summary>
        /// Creates factory that can be used to construct delegating service provider.
        /// </summary>
        /// <remarks>
        /// The constructed factory compiles specialization of the service provider on-the-fly
        /// using dynamic code compilation technique when applicable. If dynamic code compilation
        /// is not supported then the factory uses the implementation backed by dictionary.
        /// </remarks>
        /// <param name="types">The array of supported types by the service provider.</param>
        /// <returns>The delegate that can be used to instantiate specialized service provider.</returns>
        public static Func<IReadOnlyList<object>, IServiceProvider, IServiceProvider> CreateDelegatingFactory(params Type[] types)
            => RuntimeFeature.IsDynamicCodeCompiled ? CreateDelegatingResolver(types).Create : types.Create;

        /// <summary>
        /// Creates factory that can be used to construct the service provider.
        /// </summary>
        /// <typeparam name="T">The type of the service that can be returned by the provider.</typeparam>
        /// <returns>The factory that can be used to construct the service provider.</returns>
        public static Func<T, IServiceProvider> CreateFactory<T>()
            where T : notnull
            => CreateFactory(typeof(T)).Create;

        /// <summary>
        /// Creates factory that can be used to construct delegating service provider.
        /// </summary>
        /// <typeparam name="T">The type of the service that can be returned by the provider.</typeparam>
        /// <returns>The factory that can be used to construct the service provider.</returns>
        public static Func<T, IServiceProvider, IServiceProvider> CreateDelegatingFactory<T>()
            where T : notnull
            => CreateDelegatingFactory(typeof(T)).Create;

        /// <summary>
        /// Creates factory that can be used to construct the service provider.
        /// </summary>
        /// <typeparam name="T1">The type of the first service that can be returned by the provider.</typeparam>
        /// <typeparam name="T2">The type of the second service that can be returned by the provider.</typeparam>
        /// <returns>The factory that can be used to construct the service provider.</returns>
        public static Func<T1, T2, IServiceProvider> CreateFactory<T1, T2>()
            where T1 : notnull
            where T2 : notnull
            => CreateFactory(typeof(T1), typeof(T2)).Create;

        /// <summary>
        /// Creates factory that can be used to construct delegating service provider.
        /// </summary>
        /// <typeparam name="T1">The type of the first service that can be returned by the provider.</typeparam>
        /// <typeparam name="T2">The type of the second service that can be returned by the provider.</typeparam>
        /// <returns>The factory that can be used to construct the service provider.</returns>
        public static Func<T1, T2, IServiceProvider, IServiceProvider> CreateDelegatingFactory<T1, T2>()
            where T1 : notnull
            where T2 : notnull
            => CreateDelegatingFactory(typeof(T1), typeof(T2)).Create;

        /// <summary>
        /// Creates factory that can be used to construct the service provider.
        /// </summary>
        /// <typeparam name="T1">The type of the first service that can be returned by the provider.</typeparam>
        /// <typeparam name="T2">The type of the second service that can be returned by the provider.</typeparam>
        /// <typeparam name="T3">The type of the third service that can be returned by the provider.</typeparam>
        /// <returns>The factory that can be used to construct the service provider.</returns>
        public static Func<T1, T2, T3, IServiceProvider> CreateFactory<T1, T2, T3>()
            where T1 : notnull
            where T2 : notnull
            where T3 : notnull
            => CreateFactory(typeof(T1), typeof(T2), typeof(T3)).Create;

        /// <summary>
        /// Creates factory that can be used to construct delegating service provider.
        /// </summary>
        /// <typeparam name="T1">The type of the first service that can be returned by the provider.</typeparam>
        /// <typeparam name="T2">The type of the second service that can be returned by the provider.</typeparam>
        /// <typeparam name="T3">The type of the third service that can be returned by the provider.</typeparam>
        /// <returns>The factory that can be used to construct the service provider.</returns>
        public static Func<T1, T2, T3, IServiceProvider, IServiceProvider> CreateDelegatingFactory<T1, T2, T3>()
            where T1 : notnull
            where T2 : notnull
            where T3 : notnull
            => CreateDelegatingFactory(typeof(T1), typeof(T2), typeof(T3)).Create;

        /// <summary>
        /// Creates factory that can be used to construct the service provider.
        /// </summary>
        /// <typeparam name="T1">The type of the first service that can be returned by the provider.</typeparam>
        /// <typeparam name="T2">The type of the second service that can be returned by the provider.</typeparam>
        /// <typeparam name="T3">The type of the third service that can be returned by the provider.</typeparam>
        /// <typeparam name="T4">The type of the fourth service that can be returned by the provider.</typeparam>
        /// <returns>The factory that can be used to construct the service provider.</returns>
        public static Func<T1, T2, T3, T4, IServiceProvider> CreateFactory<T1, T2, T3, T4>()
            where T1 : notnull
            where T2 : notnull
            where T3 : notnull
            where T4 : notnull
            => CreateFactory(typeof(T1), typeof(T2), typeof(T3), typeof(T4)).Create;

        /// <summary>
        /// Creates factory that can be used to construct delegating service provider.
        /// </summary>
        /// <typeparam name="T1">The type of the first service that can be returned by the provider.</typeparam>
        /// <typeparam name="T2">The type of the second service that can be returned by the provider.</typeparam>
        /// <typeparam name="T3">The type of the third service that can be returned by the provider.</typeparam>
        /// <typeparam name="T4">The type of the fourth service that can be returned by the provider.</typeparam>
        /// <returns>The factory that can be used to construct the service provider.</returns>
        public static Func<T1, T2, T3, T4, IServiceProvider, IServiceProvider> CreateDelegatingFactory<T1, T2, T3, T4>()
            where T1 : notnull
            where T2 : notnull
            where T3 : notnull
            where T4 : notnull
            => CreateDelegatingFactory(typeof(T1), typeof(T2), typeof(T3), typeof(T4)).Create;

        /// <summary>
        /// Creates factory that can be used to construct the service provider.
        /// </summary>
        /// <typeparam name="T1">The type of the first service that can be returned by the provider.</typeparam>
        /// <typeparam name="T2">The type of the second service that can be returned by the provider.</typeparam>
        /// <typeparam name="T3">The type of the third service that can be returned by the provider.</typeparam>
        /// <typeparam name="T4">The type of the fourth service that can be returned by the provider.</typeparam>
        /// <typeparam name="T5">The type of the fifth service that can be returned by the provider.</typeparam>
        /// <returns>The factory that can be used to construct the service provider.</returns>
        public static Func<T1, T2, T3, T4, T5, IServiceProvider> CreateFactory<T1, T2, T3, T4, T5>()
            where T1 : notnull
            where T2 : notnull
            where T3 : notnull
            where T4 : notnull
            where T5 : notnull
            => CreateFactory(typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5)).Create;

        /// <summary>
        /// Creates factory that can be used to construct delegating service provider.
        /// </summary>
        /// <typeparam name="T1">The type of the first service that can be returned by the provider.</typeparam>
        /// <typeparam name="T2">The type of the second service that can be returned by the provider.</typeparam>
        /// <typeparam name="T3">The type of the third service that can be returned by the provider.</typeparam>
        /// <typeparam name="T4">The type of the fourth service that can be returned by the provider.</typeparam>
        /// <typeparam name="T5">The type of the fifth service that can be returned by the provider.</typeparam>
        /// <returns>The factory that can be used to construct the service provider.</returns>
        public static Func<T1, T2, T3, T4, T5, IServiceProvider, IServiceProvider> CreateDelegatingFactory<T1, T2, T3, T4, T5>()
            where T1 : notnull
            where T2 : notnull
            where T3 : notnull
            where T4 : notnull
            where T5 : notnull
            => CreateDelegatingFactory(typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5)).Create;

        /// <summary>
        /// Creates service provider from the tuple.
        /// </summary>
        /// <param name="tuple">The tuple containing services.</param>
        /// <param name="fallback">The fallback provider used for service resolution if tuple doesn't contain the requested service.</param>
        /// <typeparam name="T">The tuple type representing a set of services.</typeparam>
        /// <returns>The service provider constructed from the tuple.</returns>
        public static IServiceProvider FromTuple<T>(T tuple, IServiceProvider? fallback = null)
            where T : struct, ITuple
            => new CachedServiceProvider<T>(tuple, fallback);

        /// <summary>
        /// Creates service provider containing the single service.
        /// </summary>
        /// <param name="service">The service instance.</param>
        /// <param name="fallback">The fallback provider used for service resolution if requested service type is not supported by the constructed provider.</param>
        /// <typeparam name="T">The type of the service.</typeparam>
        /// <returns>The service provider.</returns>
        public static IServiceProvider Create<T>(T service, IServiceProvider? fallback = null)
            where T : notnull
            => new CachedServiceProvider(fallback, Registration(service));

        /// <summary>
        /// Creates service provider containing the two services.
        /// </summary>
        /// <param name="service1">The first service.</param>
        /// <param name="service2">The second service.</param>
        /// <param name="fallback">The fallback provider used for service resolution if requested service type is not supported by the constructed provider.</param>
        /// <typeparam name="T1">The type of the first service.</typeparam>
        /// <typeparam name="T2">The type of the second service.</typeparam>
        /// <returns>The service provider.</returns>
        public static IServiceProvider Create<T1, T2>(T1 service1, T2 service2, IServiceProvider? fallback = null)
            where T1 : notnull
            where T2 : notnull
            => new CachedServiceProvider(fallback, Registration(service1), Registration(service2));

        /// <summary>
        /// Creates service provider containing the three services.
        /// </summary>
        /// <param name="service1">The first service.</param>
        /// <param name="service2">The second service.</param>
        /// <param name="service3">The third service.</param>
        /// <param name="fallback">The fallback provider used for service resolution if requested service type is not supported by the constructed provider.</param>
        /// <typeparam name="T1">The type of the first service.</typeparam>
        /// <typeparam name="T2">The type of the second service.</typeparam>
        /// <typeparam name="T3">The type of the third service.</typeparam>
        /// <returns>The service provider.</returns>
        public static IServiceProvider Create<T1, T2, T3>(T1 service1, T2 service2, T3 service3, IServiceProvider? fallback = null)
            where T1 : notnull
            where T2 : notnull
            where T3 : notnull
            => new CachedServiceProvider(fallback, Registration(service1), Registration(service2), Registration(service3));

        /// <summary>
        /// Creates service provider containing the four services.
        /// </summary>
        /// <param name="service1">The first service.</param>
        /// <param name="service2">The second service.</param>
        /// <param name="service3">The third service.</param>
        /// <param name="service4">The fourth service.</param>
        /// <param name="fallback">The fallback provider used for service resolution if requested service type is not supported by the constructed provider.</param>
        /// <typeparam name="T1">The type of the first service.</typeparam>
        /// <typeparam name="T2">The type of the second service.</typeparam>
        /// <typeparam name="T3">The type of the third service.</typeparam>
        /// <typeparam name="T4">The type of the fourth service.</typeparam>
        /// <returns>The service provider.</returns>
        public static IServiceProvider Create<T1, T2, T3, T4>(T1 service1, T2 service2, T3 service3, T4 service4, IServiceProvider? fallback = null)
            where T1 : notnull
            where T2 : notnull
            where T3 : notnull
            where T4 : notnull
            => new CachedServiceProvider(fallback, Registration(service1), Registration(service2), Registration(service3), Registration(service4));

        /// <summary>
        /// Creates service provider containing the four services.
        /// </summary>
        /// <param name="service1">The first service.</param>
        /// <param name="service2">The second service.</param>
        /// <param name="service3">The third service.</param>
        /// <param name="service4">The fourth service.</param>
        /// <param name="service5">The fifth service.</param>
        /// <param name="fallback">The fallback provider used for service resolution if requested service type is not supported by the constructed provider.</param>
        /// <typeparam name="T1">The type of the first service.</typeparam>
        /// <typeparam name="T2">The type of the second service.</typeparam>
        /// <typeparam name="T3">The type of the third service.</typeparam>
        /// <typeparam name="T4">The type of the fourth service.</typeparam>
        /// <typeparam name="T5">The type of the fifth service.</typeparam>
        /// <returns>The service provider.</returns>
        public static IServiceProvider Create<T1, T2, T3, T4, T5>(T1 service1, T2 service2, T3 service3, T4 service4, T5 service5,
            IServiceProvider? fallback = null)
            where T1 : notnull
            where T2 : notnull
            where T3 : notnull
            where T4 : notnull
            where T5 : notnull
            => new CachedServiceProvider(fallback, Registration(service1), Registration(service2), Registration(service3), Registration(service4),
                Registration(service5));
    }

    private static CachedServiceProvider Create(this IReadOnlyList<Type> types, IReadOnlyList<object> services, IServiceProvider? fallback)
    {
        var cache = new CachedServiceProvider(types.Count, fallback);
        for (var i = 0; i < types.Count; i++)
            cache.Add(types[i], services[i]);

        cache.TrimExcess();
        return cache;
    }

    private static CachedServiceProvider Create(this IReadOnlyList<Type> types, IReadOnlyList<object> services)
        => Create(types, services, null);

    private static SwitchExpression MakeResolver(IReadOnlyList<Type> types, ParameterExpression requestedType, ParameterExpression values, Expression defaultResolution)
    {
        // construct selectors
        var cases = new List<SwitchCase>(types.Count);
        var indexer = IReadOnlyList<object>.Indexer.Method;
        for (var i = 0; i < types.Count; i++)
            cases.Insert(i, Expression.SwitchCase(Expression.Call(values, indexer, Expression.Constant(i)), Expression.Constant(types[i])));

        return Expression.Switch(typeof(object), requestedType, defaultResolution, null, cases);
    }

    private static Func<Type, IReadOnlyList<object?>, object?> CreateResolver(IReadOnlyList<Type> types)
    {
        var requestedType = Expression.Parameter(typeof(Type));
        var values = Expression.Parameter(typeof(object?[]));
        var resolverBody = MakeResolver(types, requestedType, values, Expression.Constant(null, typeof(object)));
        return Expression.Lambda<Func<Type, IReadOnlyList<object?>, object?>>(resolverBody, false, requestedType, values).Compile();
    }

    private static CompiledServiceProvider Create(this Func<Type, IReadOnlyList<object?>, object?> resolver, IReadOnlyList<object?> services)
        => new(resolver, services);

    [RequiresDynamicCode("Dynamic code compilation is required for faster lookup")]
    [RequiresUnreferencedCode("Calls System.Linq.Expressions.Expression.Call(Expression, String, Type[], params Expression[])")]
    private static Func<Type, IReadOnlyList<object>, IServiceProvider, object?> CreateDelegatingResolver(IReadOnlyList<Type> types)
    {
        var requestedType = Expression.Parameter(typeof(Type));
        var values = Expression.Parameter(typeof(object[]));
        var fallbackResolver = Expression.Parameter(typeof(IServiceProvider));
        var resolverBody = MakeResolver(types, requestedType, values, Expression.Call(fallbackResolver, nameof(IServiceProvider.GetService), [], requestedType));
        return Expression.Lambda<Func<Type, IReadOnlyList<object>, IServiceProvider, object?>>(resolverBody, false, requestedType, values, fallbackResolver).Compile();
    }

    private static DelegatingServiceProvider Create(this Func<Type, IReadOnlyList<object>, IServiceProvider, object?> resolver, IReadOnlyList<object> services, IServiceProvider fallback)
        => new(resolver, services, fallback);

    private static IServiceProvider Create<T>(this Func<IReadOnlyList<object>, IServiceProvider> factory, T service)
        where T : notnull
        => factory([service]);

    private static IServiceProvider Create<T>(this Func<IReadOnlyList<object>, IServiceProvider, IServiceProvider> factory, T service, IServiceProvider fallback)
        where T : notnull
        => factory([service], fallback);

    private static IServiceProvider Create<T1, T2>(this Func<IReadOnlyList<object>, IServiceProvider> factory, T1 service1, T2 service2)
        where T1 : notnull
        where T2 : notnull
        => factory([service1, service2]);

    private static IServiceProvider Create<T1, T2>(this Func<IReadOnlyList<object>, IServiceProvider, IServiceProvider> factory, T1 service1, T2 service2,
        IServiceProvider fallback)
        where T1 : notnull
        where T2 : notnull
        => factory([service1, service2], fallback);

    private static IServiceProvider Create<T1, T2, T3>(this Func<IReadOnlyList<object>, IServiceProvider> factory, T1 service1, T2 service2, T3 service3)
        where T1 : notnull
        where T2 : notnull
        where T3 : notnull
        => factory([service1, service2, service3]);

    private static IServiceProvider Create<T1, T2, T3>(this Func<IReadOnlyList<object>, IServiceProvider, IServiceProvider> factory, T1 service1, T2 service2,
        T3 service3, IServiceProvider fallback)
        where T1 : notnull
        where T2 : notnull
        where T3 : notnull
        => factory([service1, service2, service3], fallback);

    private static IServiceProvider Create<T1, T2, T3, T4>(this Func<IReadOnlyList<object>, IServiceProvider> factory, T1 service1, T2 service2, T3 service3,
        T4 service4)
        where T1 : notnull
        where T2 : notnull
        where T3 : notnull
        where T4 : notnull
        => factory([service1, service2, service3, service4]);

    private static IServiceProvider Create<T1, T2, T3, T4>(this Func<IReadOnlyList<object>, IServiceProvider, IServiceProvider> factory, T1 service1, T2 service2,
        T3 service3, T4 service4, IServiceProvider fallback)
        where T1 : notnull
        where T2 : notnull
        where T3 : notnull
        where T4 : notnull
        => factory([service1, service2, service3, service4], fallback);

    private static IServiceProvider Create<T1, T2, T3, T4, T5>(this Func<IReadOnlyList<object>, IServiceProvider> factory, T1 service1, T2 service2, T3 service3,
        T4 service4, T5 service5)
        where T1 : notnull
        where T2 : notnull
        where T3 : notnull
        where T4 : notnull
        where T5 : notnull
        => factory([service1, service2, service3, service4, service5]);

    private static IServiceProvider Create<T1, T2, T3, T4, T5>(this Func<IReadOnlyList<object>, IServiceProvider, IServiceProvider> factory, T1 service1,
        T2 service2, T3 service3, T4 service4, T5 service5, IServiceProvider fallback)
        where T1 : notnull
        where T2 : notnull
        where T3 : notnull
        where T4 : notnull
        where T5 : notnull
        => factory([service1, service2, service3, service4, service5], fallback);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static KeyValuePair<Type, object?> Registration<T>(T service)
        where T : notnull
        => new(typeof(T), service);
}