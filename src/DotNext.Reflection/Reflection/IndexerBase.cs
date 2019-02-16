using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace DotNext.Reflection
{
	public abstract class IndexerBase<A, V>: PropertyInfo, IProperty, IEquatable<IndexerBase<A, V>>, IEquatable<PropertyInfo>
		where A: struct
	{
		private readonly PropertyInfo property;

		private protected IndexerBase(PropertyInfo property)
			=> this.property = property;

		public override object GetValue(object obj, object[] index) => property.GetValue(obj, index);

		public override void SetValue(object obj, object value, object[] index) => property.SetValue(obj, value, index);

		public sealed override string Name => property.Name;

		public sealed override bool CanRead => property.CanRead;

		public sealed override bool CanWrite => property.CanWrite;

		public sealed override MethodInfo GetMethod => property.GetMethod;

		public sealed override PropertyAttributes Attributes => property.Attributes;

		public sealed override Type PropertyType => property.PropertyType;

		public sealed override MethodInfo SetMethod => property.SetMethod;

		public sealed override MethodInfo[] GetAccessors(bool nonPublic) => property.GetAccessors(nonPublic);

		public sealed override object GetConstantValue() => property.GetConstantValue();

		public sealed override MethodInfo GetGetMethod(bool nonPublic) => property.GetGetMethod(nonPublic);

		public sealed override ParameterInfo[] GetIndexParameters() => property.GetIndexParameters();

		public sealed override Type[] GetOptionalCustomModifiers() => property.GetOptionalCustomModifiers();

		public sealed override object GetRawConstantValue() => property.GetRawConstantValue();

		public sealed override Type[] GetRequiredCustomModifiers() => property.GetRequiredCustomModifiers();

		public sealed override MethodInfo GetSetMethod(bool nonPublic) => property.GetSetMethod(nonPublic);

		public override object GetValue(object obj, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture)
			=> property.GetValue(obj, invokeAttr, binder, index, culture);

		public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture)
			=> property.SetValue(obj, value, invokeAttr, binder, index, culture);

		public sealed override Type DeclaringType => property.DeclaringType;

		public sealed override MemberTypes MemberType => property.MemberType;

		public sealed override Type ReflectedType => property.ReflectedType;

		public sealed override object[] GetCustomAttributes(bool inherit) => property.GetCustomAttributes(inherit);
		public sealed override object[] GetCustomAttributes(Type attributeType, bool inherit) => property.GetCustomAttributes(attributeType, inherit);

		public sealed override bool IsDefined(Type attributeType, bool inherit) => property.IsDefined(attributeType, inherit);

		public sealed override int MetadataToken => property.MetadataToken;

		public sealed override Module Module => property.Module;

		public sealed override IList<CustomAttributeData> GetCustomAttributesData() => property.GetCustomAttributesData();

		public sealed override IEnumerable<CustomAttributeData> CustomAttributes => property.CustomAttributes;

		PropertyInfo IMember<PropertyInfo>.RuntimeMember => property;

		public bool Equals(PropertyInfo other) => property == other;

		public bool Equals(IndexerBase<A, V> other)
			=> other != null &&
				GetType() == other.GetType() &&
				property == other.property;

		public override bool Equals(object other)
		{
			switch (other)
			{
				case PropertyBase<V> property:
					return Equals(property);
				case PropertyInfo property:
					return Equals(property);
				default:
					return false;
			}
		}

		public override int GetHashCode() => property.GetHashCode();

		public static bool operator ==(IndexerBase<A, V> first, IndexerBase<A, V> second) => Equals(first, second);

		public static bool operator !=(IndexerBase<A, V> first, IndexerBase<A, V> second) => !Equals(first, second);

		public override string ToString() => property.ToString();
	}

	/// <summary>
	/// Represents statix indexer property.
	/// </summary>
	/// <typeparam name="A">A structure representing parameters of indexer.</typeparam>
	/// <typeparam name="V">Property value.</typeparam>
	public sealed class Indexer<A, V>: IndexerBase<A, V>
		where A: struct
	{
		private const BindingFlags PublicFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy;
		private const BindingFlags NonPublicFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

		public delegate V Getter(in A arguments);
		public delegate void Setter(in A arguments, V value);

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
	
		public V this[in A arguments]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => GetMethod is null ? throw new InvalidOperationException(ExceptionMessages.PropertyWithoutGetter(Name)) : GetMethod.Invoker(arguments);
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set
			{
                if (SetMethod is null)
                    throw new InvalidOperationException(ExceptionMessages.PropertyWithoutSetter(Name));
                else
                    SetMethod.Invoker(arguments, value);
			}
		}

        public static implicit operator Getter(Indexer<A, V> indexer) => indexer?.GetMethod;

        public static implicit operator Setter(Indexer<A, V> indexer) => indexer?.SetMethod;
		
		internal static Indexer<A, V> Reflect<T>(string propertyName, bool nonPublic)
		{
			var property = typeof(T).GetProperty(propertyName, nonPublic ? NonPublicFlags : PublicFlags);
			if (property.PropertyType != typeof(V))
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
	}

    /// <summary>
	/// Represents statix indexer property.
	/// </summary>
    /// <typeparam name="T">Type of instance with indexer property.</typeparam>
	/// <typeparam name="A">A structure representing parameters of indexer.</typeparam>
	/// <typeparam name="V">Property value.</typeparam>
    public sealed class Indexer<T, A, V>: IndexerBase<A, V>
        where A: struct
    {
        private const BindingFlags PublicFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy;
        private const BindingFlags NonPublicFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        public delegate V Getter(in T instance, in A arguments);
        public delegate void Setter(in T instance, in A arguments, V value);

        private Indexer(PropertyInfo property, Method<Getter> getter, Method<Setter> setter)
            : base(property)
        {
            GetMethod = getter;
            SetMethod = setter;
        }

        public static implicit operator Getter(Indexer<T, A, V> indexer) => indexer?.GetMethod;

        public static implicit operator Setter(Indexer<T, A, V> indexer) => indexer?.SetMethod;

        /// <summary>
        /// Gets indexer property getter.
        /// </summary>
        public new Method<Getter> GetMethod { get; }

        /// <summary>
        /// Gets indexer property setter.
        /// </summary>
        public new Method<Setter> SetMethod { get; }

		public V this[in T instance, in A arguments]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => GetMethod is null ? throw new InvalidOperationException(ExceptionMessages.PropertyWithoutGetter(Name)) : GetMethod.Invoker(instance, arguments);
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set
			{
				if (SetMethod is null)
					throw new InvalidOperationException(ExceptionMessages.PropertyWithoutSetter(Name));
				else
					SetMethod.Invoker(instance, arguments, value);
			}
		}

		internal static Indexer<T, A, V> Reflect(string propertyName, bool nonPublic)
		{
			var property = typeof(T).GetProperty(propertyName, nonPublic ? NonPublicFlags : PublicFlags);
			if (property.PropertyType != typeof(V))
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
    }
}
