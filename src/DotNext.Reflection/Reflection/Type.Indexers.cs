namespace DotNext.Reflection
{
    public static partial class Type<T>
    {
        /// <summary>
        /// Provides access to indexer property declared in type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="A">A structure representing parameters of indexer.</typeparam>
	    /// <typeparam name="V">Property value.</typeparam>
        public static class Indexer<A, V>
            where A : struct
        {
            private const string DefaultIndexerName = "Item";

            /// <summary>
            /// Reflects static indexer property.
            /// </summary>
            /// <param name="propertyName">The name of the indexer property.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public property.</param>
			/// <returns>The reflected property; or <see langword="null"/>, if property doesn't exist.</returns>
            public static Reflection.Indexer<A, V> GetStatic(string propertyName, bool nonPublic = false)
                => Reflection.Indexer<A, V>.GetOrCreate<T>(propertyName, nonPublic);

            /// <summary>
            /// Reflects static indexer property.
            /// </summary>
            /// <param name="propertyName">The name of the indexer property.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public property.</param>
            /// <returns>The reflected indexer property.</returns>
            /// <exception cref="MissingPropertyException">The property doesn't exist.</exception>
            public static Reflection.Indexer<A, V> RequireStatic(string propertyName, bool nonPublic = false)
                => GetStatic(propertyName, nonPublic) ?? throw MissingPropertyException.Create<T, V>(propertyName);

            /// <summary>
            /// Reflects instance indexer property.
            /// </summary>
            /// <param name="propertyName">The name of the indexer property.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public property.</param>
            /// <returns>The reflected property; or <see langword="null"/>, if property doesn't exist.</returns>
            public static Indexer<T, A, V> Get(string propertyName = DefaultIndexerName, bool nonPublic = false)
                => Indexer<T, A, V>.GetOrCreate(propertyName, nonPublic);

            /// <summary>
            /// Reflects instance indexer property.
            /// </summary>
            /// <param name="propertyName">The name of the indexer property.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public property.</param>
            /// <returns>The reflected indexer property.</returns>
            /// <exception cref="MissingPropertyException">The property doesn't exist.</exception>
            public static Indexer<T, A, V> Require(string propertyName = DefaultIndexerName, bool nonPublic = false)
                => Get(propertyName, nonPublic) ?? throw MissingPropertyException.Create<T, V>(propertyName);
        }
    }
}
