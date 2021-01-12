namespace DotNext.Reflection
{
    public static partial class Type<T>
    {
        /// <summary>
        /// Provides access to indexer property declared in type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="TIndicies">A structure representing parameters of indexer.</typeparam>
        /// <typeparam name="TValue">Property value.</typeparam>
        public static class Indexer<TIndicies, TValue>
            where TIndicies : struct
        {
            private const string DefaultIndexerName = "Item";

            /// <summary>
            /// Reflects static indexer property.
            /// </summary>
            /// <param name="propertyName">The name of the indexer property.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public property.</param>
            /// <returns>The reflected property; or <see langword="null"/>, if property doesn't exist.</returns>
            public static Reflection.Indexer<TIndicies, TValue>? GetStatic(string propertyName, bool nonPublic = false)
                => Reflection.Indexer<TIndicies, TValue>.GetOrCreate<T>(propertyName, nonPublic);

            /// <summary>
            /// Reflects static indexer property.
            /// </summary>
            /// <param name="propertyName">The name of the indexer property.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public property.</param>
            /// <returns>The reflected indexer property.</returns>
            /// <exception cref="MissingPropertyException">The property doesn't exist.</exception>
            public static Reflection.Indexer<TIndicies, TValue> RequireStatic(string propertyName, bool nonPublic = false)
                => GetStatic(propertyName, nonPublic) ?? throw MissingPropertyException.Create<T, TValue>(propertyName);

            /// <summary>
            /// Reflects instance indexer property.
            /// </summary>
            /// <param name="propertyName">The name of the indexer property.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public property.</param>
            /// <returns>The reflected property; or <see langword="null"/>, if property doesn't exist.</returns>
            public static Indexer<T, TIndicies, TValue>? Get(string propertyName = DefaultIndexerName, bool nonPublic = false)
                => Indexer<T, TIndicies, TValue>.GetOrCreate(propertyName, nonPublic);

            /// <summary>
            /// Reflects instance indexer property.
            /// </summary>
            /// <param name="propertyName">The name of the indexer property.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public property.</param>
            /// <returns>The reflected indexer property.</returns>
            /// <exception cref="MissingPropertyException">The property doesn't exist.</exception>
            public static Indexer<T, TIndicies, TValue> Require(string propertyName = DefaultIndexerName, bool nonPublic = false)
                => Get(propertyName, nonPublic) ?? throw MissingPropertyException.Create<T, TValue>(propertyName);
        }
    }
}
