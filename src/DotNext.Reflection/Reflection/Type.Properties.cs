namespace DotNext.Reflection
{
    public static partial class Type<T>
    {
        /// <summary>
        /// Provides access to property declared in type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="V">Type of property.</typeparam>
        public static class Property<V>
        {
            /// <summary>
            /// Reflects instance property.
            /// </summary>
            /// <param name="propertyName">Name of property.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public property.</param>
            /// <returns>Property field; or <see langword="null"/>, if property doesn't exist.</returns>
            public static Property<T, V> Get(string propertyName, bool nonPublic = false)
                => Property<T, V>.GetOrCreate(propertyName, nonPublic);

            /// <summary>
            /// Reflects instance property.
            /// </summary>
            /// <param name="propertyName">Name of property.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public property.</param>
            /// <returns>Instance property.</returns>
            /// <exception cref="MissingPropertyException">Property doesn't exist.</exception>
            public static Property<T, V> Require(string propertyName, bool nonPublic = false)
                => Get(propertyName, nonPublic) ?? throw MissingPropertyException.Create<T, V>(propertyName);

            /// <summary>
            /// Reflects instance property getter method.
            /// </summary>
            /// <param name="propertyName">The name of the property.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public property.</param>
            /// <returns>The reflected getter method; or <see langword="null"/>, if getter method doesn't exist.</returns>
			public static Reflection.Method<MemberGetter<T, V>> GetGetter(string propertyName, bool nonPublic = false)
                => Get(propertyName, nonPublic)?.GetMethod;

            /// <summary>
            /// Reflects instance property getter method.
            /// </summary>
            /// <param name="propertyName">The name of the property.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public property.</param>
            /// <returns>The reflected getter method.</returns>
            /// <exception cref="MissingMethodException">The getter doesn't exist.</exception>
            public static Reflection.Method<MemberGetter<T, V>> RequireGetter(string propertyName, bool nonPublic = false)
                => GetGetter(propertyName, nonPublic) ?? throw MissingMethodException.Create<T, MemberGetter<T, V>>(propertyName.ToGetterName());

            /// <summary>
            /// Reflects instance property setter method.
            /// </summary>
            /// <param name="propertyName">The name of the property.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public property.</param>
            /// <returns>The reflected setter method; or <see langword="null"/>, if setter method doesn't exist.</returns>
            public static Reflection.Method<MemberSetter<T, V>> GetSetter(string propertyName, bool nonPublic = false)
                => Get(propertyName, nonPublic)?.SetMethod;

            /// <summary>
            /// Reflects instance property setter method.
            /// </summary>
            /// <param name="propertyName">The name of the property.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public property.</param>
            /// <returns>The reflected setter method.</returns>
            /// <exception cref="MissingMethodException">The setter doesn't exist.</exception>
            public static Reflection.Method<MemberSetter<T, V>> RequireSetter(string propertyName, bool nonPublic = false)
                => GetSetter(propertyName, nonPublic) ?? throw MissingMethodException.Create<T, MemberSetter<T, V>>(propertyName.ToSetterName());

            /// <summary>
            /// Reflects static property.
            /// </summary>
            /// <param name="propertyName">Name of property.</param>
            /// <param name="nonPublic">True to reflect non-public property.</param>
            /// <returns>Instance property; or <see langword="null"/>, if property doesn't exist.</returns>
            public static Reflection.Property<V> GetStatic(string propertyName, bool nonPublic = false)
                => Reflection.Property<V>.GetOrCreate<T>(propertyName, nonPublic);

            /// <summary>
            /// Reflects static property.
            /// </summary>
            /// <param name="propertyName">Name of property.</param>
            /// <param name="nonPublic">True to reflect non-public property.</param>
            /// <returns>Instance property.</returns>
            /// <exception cref="MissingPropertyException">Property doesn't exist.</exception>
            public static Reflection.Property<V> RequireStatic(string propertyName, bool nonPublic = false)
                => GetStatic(propertyName, nonPublic) ?? throw MissingFieldException.Create<T, V>(propertyName);

            /// <summary>
            /// Reflects static property getter method.
            /// </summary>
            /// <param name="propertyName">The name of the property.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public property.</param>
            /// <returns>The reflected getter method; or <see langword="null"/>, if getter method doesn't exist.</returns>
            public static Reflection.Method<MemberGetter<V>> GetStaticGetter(string propertyName, bool nonPublic = false)
                => GetStatic(propertyName, nonPublic)?.GetMethod;

            /// <summary>
            /// Reflects static property setter method.
            /// </summary>
            /// <param name="propertyName">The name of the property.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public property.</param>
            /// <returns>The reflected setter method.</returns>
            /// <exception cref="MissingMethodException">The setter doesn't exist.</exception>
            public static Reflection.Method<MemberGetter<V>> RequireStaticGetter(string propertyName, bool nonPublic = false)
                => GetStaticGetter(propertyName, nonPublic) ?? throw MissingMethodException.Create<T, MemberGetter<V>>(propertyName.ToGetterName());

            /// <summary>
            /// Reflects static property setter method.
            /// </summary>
            /// <param name="propertyName">The name of the property.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public property.</param>
            /// <returns>The reflected setter method; or <see langword="null"/>, if setter method doesn't exist.</returns>
            public static Reflection.Method<MemberSetter<V>> GetStaticSetter(string propertyName, bool nonPublic = false)
                => GetStatic(propertyName, nonPublic)?.SetMethod;

            /// <summary>
            /// Reflects static property setter method.
            /// </summary>
            /// <param name="propertyName">The name of the property.</param>
            /// <param name="nonPublic"><see langword="true"/> to reflect non-public property.</param>
            /// <returns>The reflected setter method.</returns>
            /// <exception cref="MissingMethodException">The setter doesn't exist.</exception>
            public static Reflection.Method<MemberSetter<V>> RequireStaticSetter(string propertyName, bool nonPublic = false)
                => GetStaticSetter(propertyName, nonPublic) ?? throw MissingMethodException.Create<T, MemberSetter<V>>(propertyName.ToSetterName());
        }
    }
}