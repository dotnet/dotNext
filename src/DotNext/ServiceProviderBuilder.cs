using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using static InlineIL.MethodRef;
using static InlineIL.TypeRef;
using BinaryOperator = InlineIL.BinaryOperator;

namespace DotNext
{
    /// <summary>
    /// Represents various ways for building <see cref="IServiceProvider"/> implementations.
    /// </summary>
    /// <remarks>
    /// This builder doesn't allow registration of multiple services of the same type.
    /// </remarks>
    public sealed class ServiceProviderBuilder : IConvertible<IServiceProvider>
    {
        private sealed class ServiceResolver : IServiceProvider
        {
            private readonly Func<Type, object?[], object?> resolver;
            private readonly object?[] services;

            internal ServiceResolver(Func<Type, object?[], object?> resolver, object?[] services)
            {
                this.services = services;
                this.resolver = resolver;
            }

            object? IServiceProvider.GetService(Type serviceType)
                => resolver(serviceType, services);
        }

        private sealed class DelegatingServiceResolver : IServiceProvider
        {
            private readonly Func<Type, object?[], IServiceProvider, object?> resolver;
            private readonly object?[] services;
            private readonly IServiceProvider fallback;

            internal DelegatingServiceResolver(Func<Type, object?[], IServiceProvider, object?> resolver, object?[] services, IServiceProvider fallback)
            {
                this.resolver = resolver;
                this.services = services;
                this.fallback = fallback;
            }

            object? IServiceProvider.GetService(Type serviceType)
                => resolver(serviceType, services, fallback);
        }

        private sealed class ServiceCache : Dictionary<Type, object?>, IServiceProvider
        {
            private readonly IServiceProvider? fallback;

            internal ServiceCache(IDictionary<Type, object?> services, IServiceProvider? fallback)
                : base(services)
            {
                this.fallback = fallback;
            }

            private ServiceCache(int capacity, IServiceProvider? fallback)
                : base(capacity)
            {
                this.fallback = fallback;
            }

            internal static ServiceCache Create(IReadOnlyList<Type> types, object?[] services, IServiceProvider? fallback)
            {
                var cache = new ServiceCache(types.Count, fallback);
                for (var i = 0; i < types.Count; i++)
                    cache.Add(types[i], services[i]);

                cache.TrimExcess();
                return cache;
            }

            internal static ServiceCache Create(IReadOnlyList<Type> types, object?[] services)
                => Create(types, services, null);

            object? IServiceProvider.GetService(Type serviceType)
                => TryGetValue(serviceType, out var service) ? service : fallback?.GetService(serviceType);
        }

        private sealed class EmptyProvider : IServiceProvider
        {
            object? IServiceProvider.GetService(Type serviceType) => null;
        }

        /// <summary>
        /// Represents empty service provider;
        /// </summary>
        public static readonly IServiceProvider Empty = new EmptyProvider();

        private readonly IDictionary<Type, object?> services = new Dictionary<Type, object?>();

        /// <summary>
        /// Registers service of the specified type.
        /// </summary>
        /// <remarks>
        /// This builder doesn't allow registration of multiple services of the same type.
        /// </remarks>
        /// <param name="service">The service instance.</param>
        /// <typeparam name="TService">The type of the service.</typeparam>
        /// <returns>This builder for subsequent calls.</returns>
        public ServiceProviderBuilder Add<TService>(TService service)
        {
            services.Add(typeof(TService), service);
            return this;
        }

        /// <summary>
        /// Constructs service provider.
        /// </summary>
        /// <param name="fallback">The fallback provider used for service resolution.</param>
        /// <returns>The constructed service provider.</returns>
        public IServiceProvider Build(IServiceProvider? fallback = null)
        {
            if (services.Count == 0)
                return fallback ?? Empty;

            return new ServiceCache(services, fallback);
        }

        /// <summary>
        /// Clears internal state of this builder and makes it reusable for subsequent calls.
        /// </summary>
        public void Clear() => services.Clear();

        /// <inheritdoc />
        IServiceProvider IConvertible<IServiceProvider>.Convert() => Build();

        private static SwitchExpression MakeResolver(IReadOnlyList<Type> types, ParameterExpression requestedType, ParameterExpression values, Expression defaultResolution)
        {
            // construct selectors
            var cases = new List<SwitchCase>(types.Count);
            for (var i = 0; i < types.Count; i++)
                cases.Insert(i, Expression.SwitchCase(Expression.ArrayAccess(values, Expression.Constant(i)), Expression.Constant(types[i])));

            // extract type equality operator
            Ldtoken(Operator(Type<Type>(), BinaryOperator.Equality, Type<Type>(), Type<Type>()));
            Pop(out RuntimeMethodHandle handle);
            var equalityOperator = (MethodInfo)MethodBase.GetMethodFromHandle(handle);

            return Expression.Switch(typeof(object), requestedType, defaultResolution, equalityOperator, cases);
        }

        private static Func<Type, object?[], object?> CreateResolver(IReadOnlyList<Type> types)
        {
            var requestedType = Expression.Parameter(typeof(Type));
            var values = Expression.Parameter(typeof(object?[]));
            var resolverBody = MakeResolver(types, requestedType, values, Expression.Constant(null, typeof(object)));
            return Expression.Lambda<Func<Type, object?[], object?>>(resolverBody, false, requestedType, values).Compile();
        }

        private static ServiceResolver CreateProvider(Func<Type, object?[], object?> resolver, object?[] services)
            => new ServiceResolver(resolver, services);

        private static Func<Type, object?[], IServiceProvider, object?> CreateDelegatingResolver(IReadOnlyList<Type> types)
        {
            var requestedType = Expression.Parameter(typeof(Type));
            var values = Expression.Parameter(typeof(object?[]));
            var fallbackResolver = Expression.Parameter(typeof(IServiceProvider));
            var resolverBody = MakeResolver(types, requestedType, values, Expression.Call(fallbackResolver, nameof(IServiceProvider.GetService), Array.Empty<Type>(), requestedType));
            return Expression.Lambda<Func<Type, object?[], IServiceProvider, object?>>(resolverBody, false, requestedType, values, fallbackResolver).Compile();
        }

        private static DelegatingServiceResolver CreateProvider(Func<Type, object?[], IServiceProvider, object?> resolver, object?[] services, IServiceProvider fallback)
            => new DelegatingServiceResolver(resolver, services, fallback);

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
        public static Func<object?[], IServiceProvider> CreateFactory(params Type[] types)
        {
            Func<object?[], IServiceProvider> result;

            if (RuntimeFeature.IsDynamicCodeCompiled)
            {
                Push(CreateResolver(types));
                Ldftn(Method(Type<ServiceProviderBuilder>(), nameof(CreateProvider), Type<Func<Type, object?[], object?>>(), Type<object[]>()));
                Newobj(Constructor(Type<Func<object?[], IServiceProvider>>(), Type<object>(), Type<IntPtr>()));
                Pop(out result);
            }
            else
            {
                Push(types);
                Ldftn(Method(Type<ServiceCache>(), nameof(ServiceCache.Create), Type<IReadOnlyList<Type>>(), Type<object[]>()));
                Newobj(Constructor(Type<Func<object?[], IServiceProvider>>(), Type<object>(), Type<IntPtr>()));
                Pop(out result);
            }

            return result;
        }

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
        public static Func<object?[], IServiceProvider, IServiceProvider> CreateDelegatingFactory(params Type[] types)
        {
            Func<object?[], IServiceProvider, IServiceProvider> result;

            if (RuntimeFeature.IsDynamicCodeCompiled)
            {
                Push(CreateResolver(types));
                Ldftn(Method(Type<ServiceProviderBuilder>(), nameof(CreateProvider), Type<Func<Type, object?[], IServiceProvider, object?>>(), Type<object[]>(), Type<IServiceProvider>()));
                Newobj(Constructor(Type<Func<object?[], IServiceProvider, IServiceProvider>>(), Type<object>(), Type<IntPtr>()));
                Pop(out result);
            }
            else
            {
                Push(types);
                Ldftn(Method(Type<ServiceCache>(), nameof(ServiceCache.Create), Type<IReadOnlyList<Type>>(), Type<object[]>(), Type<IServiceProvider>()));
                Newobj(Constructor(Type<Func<object?[], IServiceProvider, IServiceProvider>>(), Type<object>(), Type<IntPtr>()));
                Pop(out result);
            }

            return result;
        }
    }
}