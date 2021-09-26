namespace DotNext;

public partial class ServiceProviderFactory
{
    /// <summary>
    /// Represents builder of the service provider.
    /// </summary>
    public sealed class Builder : ISupplier<IServiceProvider>
    {
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
        public Builder Add<TService>(TService service)
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

            return new CachedServiceProvider(services, fallback);
        }

        /// <summary>
        /// Clears internal state of this builder and makes it reusable for subsequent calls.
        /// </summary>
        public void Clear() => services.Clear();

        /// <inheritdoc />
        IServiceProvider ISupplier<IServiceProvider>.Invoke() => Build();
    }
}