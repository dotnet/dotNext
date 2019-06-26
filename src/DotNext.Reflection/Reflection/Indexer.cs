using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotNext.Reflection
{
    /// <summary>
    /// Represents reflected indexer property.
    /// </summary>
    /// <typeparam name="A">The type representing indexer arguments.</typeparam>
    /// <typeparam name="V">The type of the property.</typeparam>
	public abstract class IndexerBase<A, V> : PropertyInfo, IProperty, IEquatable<PropertyInfo>
        where A : struct
    {
        private readonly PropertyInfo property;

        private protected IndexerBase(PropertyInfo property)
            => this.property = property;

        /// <summary>
        /// Returns the property value of a specified object with index values for indexed properties.
        /// </summary>
        /// <param name="obj">The object whose property value will be returned.</param>
        /// <param name="index">Index values for indexed properties.</param>
        /// <returns>The property value of the specified object.</returns>
		public override object GetValue(object obj, object[] index) => property.GetValue(obj, index);

        /// <summary>
        /// Sets the property value of a specified object with optional index values for index properties.
        /// </summary>
        /// <param name="obj">The object whose property value will be set.</param>
        /// <param name="value">The new property value.</param>
        /// <param name="index">The property value of the specified object.</param>
		public override void SetValue(object obj, object value, object[] index) => property.SetValue(obj, value, index);

        /// <summary>
        /// Gets name of the property.
        /// </summary>
		public sealed override string Name => property.Name;

        /// <summary>
        /// Gets a value indicating whether the property can be read.
        /// </summary>
		public sealed override bool CanRead => property.CanRead;

        /// <summary>
        /// Gets a value indicating whether the property can be written to.
        /// </summary>
		public sealed override bool CanWrite => property.CanWrite;

        /// <summary>
        /// Gets the get accessor for this property.
        /// </summary>
		public sealed override MethodInfo GetMethod => property.GetMethod;

        /// <summary>
        /// Gets the attributes for this property.
        /// </summary>
		public sealed override PropertyAttributes Attributes => property.Attributes;

        /// <summary>
        /// Gets the type of this property.
        /// </summary>
		public sealed override Type PropertyType => property.PropertyType;

        /// <summary>
        /// Gets the set accessor for this property.
        /// </summary>
		public sealed override MethodInfo SetMethod => property.SetMethod;

        /// <summary>
        /// Returns an array whose elements reflect the public and, if specified, non-public get and set accessors of the 
        /// property reflected by the current instance.
        /// </summary>
        /// <param name="nonPublic">Indicates whether non-public methods should be returned in the returned array.</param>
        /// <returns>An array whose elements reflect the get and set accessors of the property reflected by the current instance.</returns>
        public sealed override MethodInfo[] GetAccessors(bool nonPublic) => property.GetAccessors(nonPublic);

        /// <summary>
        /// Returns a literal value associated with the property by a compiler.
        /// </summary>
        /// <returns>The literal value associated with the property.</returns>
		public sealed override object GetConstantValue() => property.GetConstantValue();

        /// <summary>
        /// Returns the public or non-public get accessor for this property.
        /// </summary>
        /// <param name="nonPublic">Indicates whether a non-public get accessor should be returned.</param>
        /// <returns>The object representing the get accessor for this property.</returns>
		public sealed override MethodInfo GetGetMethod(bool nonPublic) => property.GetGetMethod(nonPublic);

        /// <summary>
        /// Returns an array of all the index parameters for the property.
        /// </summary>
        /// <returns>An array containing the parameters for the indexes.</returns>
		public sealed override ParameterInfo[] GetIndexParameters() => property.GetIndexParameters();

        /// <summary>
        /// Returns an array of types representing the optional custom modifiers of the property.
        /// </summary>
        /// <returns>An array of objects that identify the optional custom modifiers of the current property</returns>
        public sealed override Type[] GetOptionalCustomModifiers() => property.GetOptionalCustomModifiers();

        /// <summary>
        /// Returns a literal value associated with the property by a compiler.
        /// </summary>
        /// <returns>An object that contains the literal value associated with the property.</returns>
		public sealed override object GetRawConstantValue() => property.GetRawConstantValue();

        /// <summary>
        /// Returns an array of types representing the required custom modifiers of the property.
        /// </summary>
        /// <returns>An array of objects that identify the required custom modifiers of the current property.</returns>
        public sealed override Type[] GetRequiredCustomModifiers() => property.GetRequiredCustomModifiers();

        /// <summary>
        /// Gets the set accessor for this property.
        /// </summary>
        /// <param name="nonPublic">Indicates whether a non-public set  accessor should be returned.</param>
        /// <returns>The object representing the set accessor for this property.</returns>
		public sealed override MethodInfo GetSetMethod(bool nonPublic) => property.GetSetMethod(nonPublic);

        /// <summary>
        /// Returns the property value of a specified object with index values for indexed properties.
        /// </summary>
        /// <param name="obj">The object whose property value will be returned.</param>
        /// <param name="invokeAttr">Specifies the type of binding.</param>
        /// <param name="binder">Defines a set of properties and enables the binding, coercion of argument types, and invocation of members using reflection.</param>
        /// <param name="index">Index values for indexed properties.</param>
        /// <param name="culture">Used to govern the coercion of types.</param>
        /// <returns>The property value of the specified object.</returns>
		public override object GetValue(object obj, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture)
            => property.GetValue(obj, invokeAttr, binder, index, culture);

        /// <summary>
        /// Sets the property value for a specified object that has the specified binding, index, and culture-specific information.
        /// </summary>
        /// <param name="obj">The object whose property value will be set.</param>
        /// <param name="value">The new value of the property.</param>
        /// <param name="invokeAttr">Specifies the type of binding.</param>
        /// <param name="binder">Defines a set of properties and enables the binding, coercion of argument types, and invocation of members using reflection.</param>
        /// <param name="index">Index values for indexed properties.</param>
        /// <param name="culture">Used to govern the coercion of types.</param>
		public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture)
            => property.SetValue(obj, value, invokeAttr, binder, index, culture);

        /// <summary>
        /// Gets the class that declares this property.
        /// </summary>
		public sealed override Type DeclaringType => property.DeclaringType;

        /// <summary>
        /// Always returns <see cref="MemberTypes.Property"/>.
        /// </summary>
		public sealed override MemberTypes MemberType => property.MemberType;

        /// <summary>
        /// Gets the class object that was used to obtain this instance.
        /// </summary>
		public sealed override Type ReflectedType => property.ReflectedType;

        /// <summary>
        /// Returns an array of all custom attributes applied to this property.
        /// </summary>
        /// <param name="inherit"><see langword="true"/> to search this member's inheritance chain to find the attributes; otherwise, <see langword="false"/>.</param>
        /// <returns>An array that contains all the custom attributes applied to this property.</returns>
        public sealed override object[] GetCustomAttributes(bool inherit) => property.GetCustomAttributes(inherit);

        /// <summary>
        /// Returns an array of all custom attributes applied to this property.
        /// </summary>
        /// <param name="attributeType">The type of attribute to search for. Only attributes that are assignable to this type are returned.</param>
        /// <param name="inherit"><see langword="true"/> to search this member's inheritance chain to find the attributes; otherwise, <see langword="false"/>.</param>
        /// <returns>An array that contains all the custom attributes applied to this property.</returns>
        public sealed override object[] GetCustomAttributes(Type attributeType, bool inherit) => property.GetCustomAttributes(attributeType, inherit);

        /// <summary>
        /// Determines whether one or more attributes of the specified type or of its derived types is applied to this property.
        /// </summary>
        /// <param name="attributeType">The type of custom attribute to search for. The search includes derived types.</param>
        /// <param name="inherit"><see langword="true"/> to search this member's inheritance chain to find the attributes; otherwise, <see langword="false"/>.</param>
        /// <returns><see langword="true"/> if one or more instances of <paramref name="attributeType"/> or any of its derived types is applied to this property; otherwise, <see langword="false"/>.</returns>
        public sealed override bool IsDefined(Type attributeType, bool inherit) => property.IsDefined(attributeType, inherit);

        /// <summary>
        /// Gets a value that identifies a metadata element.
        /// </summary>
		public sealed override int MetadataToken => property.MetadataToken;

        /// <summary>
        /// Gets the module in which the type that declares the property represented by the current instance is defined.
        /// </summary>
		public sealed override Module Module => property.Module;

        /// <summary>
        /// Returns a list of custom attributes that have been applied to the target property.
        /// </summary>
        /// <returns>The data about the attributes that have been applied to the target property.</returns>
		public sealed override IList<CustomAttributeData> GetCustomAttributesData() => property.GetCustomAttributesData();

        /// <summary>
        /// Gets a collection that contains this member's custom attributes.
        /// </summary>
		public sealed override IEnumerable<CustomAttributeData> CustomAttributes => property.CustomAttributes;

        PropertyInfo IMember<PropertyInfo>.RuntimeMember => property;

        /// <summary>
        /// Determines whether this property is equal to the given property.
        /// </summary>
        /// <param name="other">Other property to compare.</param>
        /// <returns><see langword="true"/> if this object reflects the same property as the specified object; otherwise, <see langword="false"/>.</returns>
        public bool Equals(PropertyInfo other) => other is IndexerBase<A, V> property ? property.property == this.property : this.property == other;

        /// <summary>
        /// Determines whether this property is equal to the given property.
        /// </summary>
        /// <param name="other">Other property to compare.</param>
        /// <returns><see langword="true"/> if this object reflects the same property as the specified object; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other)
        {
            switch (other)
            {
                case IndexerBase<A, V> property:
                    return this.property == property.property;
                case PropertyInfo property:
                    return this.property == property;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Computes hash code uniquely identifies the reflected property.
        /// </summary>
        /// <returns>The hash code of the property.</returns>
        public override int GetHashCode() => property.GetHashCode();

        /// <summary>
        /// Returns textual representation of this property.
        /// </summary>
        /// <returns>The textual representation of this property.</returns>
        public override string ToString() => property.ToString();
    }

    /// <summary>
    /// Represents static indexer property.
    /// </summary>
    /// <typeparam name="A">A structure representing parameters of indexer.</typeparam>
    /// <typeparam name="V">Property value.</typeparam>
    public sealed class Indexer<A, V> : IndexerBase<A, V>
        where A : struct
    {
        private sealed class Cache<T> : MemberCache<PropertyInfo, Indexer<A, V>>
        {
            private protected override Indexer<A, V> Create(string propertyName, bool nonPublic) => Reflect(typeof(T), propertyName, nonPublic);
        }
        private const BindingFlags PublicFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy;
        private const BindingFlags NonPublicFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        /// <summary>
        /// Represents property getter.
        /// </summary>
        /// <param name="index">Index values for indexed properties.</param>
        /// <returns>The property value.</returns>
		public delegate V Getter(in A index);

        /// <summary>
        /// Represents property setter.
        /// </summary>
        /// <param name="index">Index values for indexed properties.</param>
        /// <param name="value">The new value of the property.</param>
		public delegate void Setter(in A index, V value);

        private Indexer(PropertyInfo property, Method<Getter> getter, Method<Setter> setter)
            : base(property)
        {
            GetMethod = getter;
            SetMethod = setter;
        }

        /// <summary>
        /// Gets indexer property getter.
        /// </summary>
        public new Method<Getter> GetMethod { get; }

        /// <summary>
        /// Gets indexer property setter.
        /// </summary>
        public new Method<Setter> SetMethod { get; }

        /// <summary>
        /// Gets or sets instance indexer property value.
        /// </summary>
        /// <param name="index">Index values for indexed properties.</param>
        /// <returns>The value of the property.</returns>
        public V this[in A index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetMethod is null ? throw new InvalidOperationException(ExceptionMessages.PropertyWithoutGetter(Name)) : GetMethod.Invoker(index);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (SetMethod is null)
                    throw new InvalidOperationException(ExceptionMessages.PropertyWithoutSetter(Name));
                else
                    SetMethod.Invoker(index, value);
            }
        }

        /// <summary>
        /// Obtains property getter.
        /// </summary>
        /// <param name="indexer">The reflected property instance.</param>
        public static implicit operator Getter(Indexer<A, V> indexer) => indexer?.GetMethod;

        /// <summary>
        /// Obtains property setter.
        /// </summary>
        /// <param name="indexer">The reflected property instance.</param>
        public static implicit operator Setter(Indexer<A, V> indexer) => indexer?.SetMethod;

        private static Indexer<A, V> Reflect(Type declaringType, string propertyName, bool nonPublic)
        {
            var property = declaringType.GetProperty(propertyName, nonPublic ? NonPublicFlags : PublicFlags);
            if (property is null || property.PropertyType != typeof(V))
                return null;
            var (actualParams, arglist, input) = Signature.Reflect<A>();
            //reflect getter
            Method<Getter> getter;
            if (property.CanRead)
                if (property.GetMethod.SignatureEquals(actualParams))
                    getter = new Method<Getter>(property.GetMethod, arglist, new[] { input });
                else
                    return null;
            else
                getter = null;
            //reflect setter
            Method<Setter> setter;
            actualParams = actualParams.Insert(property.PropertyType, actualParams.LongLength);
            if (property.CanWrite)
                if (property.SetMethod.SignatureEquals(actualParams))
                {
                    var valueParam = Expression.Parameter(property.PropertyType, "value");
                    arglist = arglist.Insert(valueParam, arglist.LongLength);
                    setter = new Method<Setter>(property.SetMethod, arglist, new[] { input, valueParam });
                }
                else
                    return null;
            else
                setter = null;

            return new Indexer<A, V>(property, getter, setter);
        }

        internal static Indexer<A, V> GetOrCreate<T>(string propertyName, bool nonPublic)
            => Cache<T>.Of<Cache<T>>(typeof(T)).GetOrCreate(propertyName, nonPublic);
    }

    /// <summary>
	/// Represents static indexer property.
	/// </summary>
    /// <typeparam name="T">Type of instance with indexer property.</typeparam>
	/// <typeparam name="A">A structure representing parameters of indexer.</typeparam>
	/// <typeparam name="V">Property value.</typeparam>
    public sealed class Indexer<T, A, V> : IndexerBase<A, V>
        where A : struct
    {
        private sealed class Cache : MemberCache<PropertyInfo, Indexer<T, A, V>>
        {
            private protected override Indexer<T, A, V> Create(string propertyName, bool nonPublic) => Reflect(propertyName, nonPublic);
        }
        private const BindingFlags PublicFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy;
        private const BindingFlags NonPublicFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        /// <summary>
        /// Represents property getter.
        /// </summary>
        /// <param name="this">The object whose property value will be returned.</param>
        /// <param name="index">Index values for indexed properties.</param>
        /// <returns>The property value.</returns>
        public delegate V Getter(in T @this, in A index);

        /// <summary>
        /// Represents property setter.
        /// </summary>
        /// <param name="this">The object whose property value will be set.</param>
        /// <param name="value">The new property value.</param>
        /// <param name="index">The property value of the specified object.</param>
        public delegate void Setter(in T @this, in A index, V value);

        private Indexer(PropertyInfo property, Method<Getter> getter, Method<Setter> setter)
            : base(property)
        {
            GetMethod = getter;
            SetMethod = setter;
        }

        /// <summary>
        /// Obtains property getter.
        /// </summary>
        /// <param name="indexer">The reflected property instance.</param>
        public static implicit operator Getter(Indexer<T, A, V> indexer) => indexer?.GetMethod;

        /// <summary>
        /// Obtains property setter.
        /// </summary>
        /// <param name="indexer">The reflected property instance.</param>
        public static implicit operator Setter(Indexer<T, A, V> indexer) => indexer?.SetMethod;

        /// <summary>
        /// Gets indexer property getter.
        /// </summary>
        public new Method<Getter> GetMethod { get; }

        /// <summary>
        /// Gets indexer property setter.
        /// </summary>
        public new Method<Setter> SetMethod { get; }

        /// <summary>
        /// Gets or sets instance property.
        /// </summary>
        /// <param name="this">The object whose property value will be set or returned.</param>
        /// <param name="index">Index values for indexed properties.</param>
        /// <returns></returns>
		public V this[in T @this, in A index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetMethod is null ? throw new InvalidOperationException(ExceptionMessages.PropertyWithoutGetter(Name)) : GetMethod.Invoker(@this, index);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (SetMethod is null)
                    throw new InvalidOperationException(ExceptionMessages.PropertyWithoutSetter(Name));
                else
                    SetMethod.Invoker(@this, index, value);
            }
        }

        private static Indexer<T, A, V> Reflect(string propertyName, bool nonPublic)
        {
            var property = typeof(T).GetProperty(propertyName, nonPublic ? NonPublicFlags : PublicFlags);
            if (property?.DeclaringType is null || property.PropertyType != typeof(V))
                return null;
            var (actualParams, arglist, input) = Signature.Reflect<A>();
            var thisParam = Expression.Parameter(property.DeclaringType.MakeByRefType(), "this");
            //reflect getter
            Method<Getter> getter;
            if (property.CanRead)
                if (property.GetMethod.SignatureEquals(actualParams))
                    getter = new Method<Getter>(property.GetMethod, thisParam, arglist, new[] { input });
                else
                    return null;
            else
                getter = null;
            //reflect setter
            Method<Setter> setter;
            actualParams = actualParams.Insert(property.PropertyType, actualParams.LongLength);
            if (property.CanWrite)
                if (property.SetMethod.SignatureEquals(actualParams))
                {
                    var valueParam = Expression.Parameter(property.PropertyType, "value");
                    arglist = arglist.Insert(valueParam, arglist.LongLength);
                    setter = new Method<Setter>(property.SetMethod, thisParam, arglist, new[] { input, valueParam });
                }
                else
                    return null;
            else
                setter = null;
            return new Indexer<T, A, V>(property, getter, setter);
        }

        internal static Indexer<T, A, V> GetOrCreate(string propertyName, bool nonPublic)
            => Cache.Of<Cache>(typeof(T)).GetOrCreate(propertyName, nonPublic);
    }
}
