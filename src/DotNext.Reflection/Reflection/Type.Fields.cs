namespace DotNext.Reflection
{
    public static partial class Type<T>
    {
        /// <summary>
        /// Provides typed access to instance field declared in type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="V">Type of field value.</typeparam>
        public static class Field<V>
        {
            /// <summary>
            /// Gets instance field.
            /// </summary>
            /// <param name="fieldName">Name of field.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public field.</param>
            /// <returns>Instance field; or <see langword="null"/>, if field doesn't exist.</returns>
            public static Field<T, V> Get(string fieldName, bool nonPublic = false)
                => Field<T, V>.GetOrCreate(fieldName, nonPublic);

            /// <summary>
            /// Gets instance field.
            /// </summary>
            /// <param name="fieldName">Name of field.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public field.</param>
            /// <returns>Instance field.</returns>
            /// <exception cref="MissingEventException">Field doesn't exist.</exception>
            public static Field<T, V> Require(string fieldName, bool nonPublic = false)
                => Get(fieldName, nonPublic) ?? throw MissingFieldException.Create<T, V>(fieldName);

            /// <summary>
            /// Gets static field.
            /// </summary>
            /// <param name="fieldName">Name of field.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public field.</param>
            /// <returns>Instance field; or <see langword="null"/>, if field doesn't exist.</returns>
            public static Reflection.Field<V> GetStatic(string fieldName, bool nonPublic = false)
                => Reflection.Field<V>.GetOrCreate<T>(fieldName, nonPublic);

            /// <summary>
            /// Gets static field.
            /// </summary>
            /// <param name="fieldName">Name of field.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public field.</param>
            /// <returns>Instance field.</returns>
            /// <exception cref="MissingEventException">Field doesn't exist.</exception>
            public static Reflection.Field<V> RequireStatic(string fieldName, bool nonPublic = false)
                => GetStatic(fieldName, nonPublic) ?? throw MissingFieldException.Create<T, V>(fieldName);
        }
    }
}