using System;
using System.Globalization;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotNext.Reflection
{
    /// <summary>
    /// Represents property.
    /// </summary>
    /// <typeparam name="V">Type of property value.</typeparam>
    public abstract class PropertyBase<V> : PropertyInfo, IProperty, IEquatable<PropertyInfo>
    {
        private readonly PropertyInfo property;

        private protected PropertyBase(PropertyInfo property) => this.property = property;

		public abstract bool GetValue(object obj, out V value);
		public abstract bool SetValue(object obj, V value);

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

        public bool Equals(PropertyInfo other) => other is PropertyBase<V> property ? property.property == this.property : this.property == other;

        public override bool Equals(object other)
        {
            switch (other)
            {
                case PropertyBase<V> property:
                    return this.property == property.property;
                case PropertyInfo property:
                    return this.property == property;
                default:
                    return false;
            }
        }

        public override int GetHashCode() => property.GetHashCode();

        public static bool operator ==(PropertyBase<V> first, PropertyBase<V> second) => Equals(first, second);

        public static bool operator !=(PropertyBase<V> first, PropertyBase<V> second) => !Equals(first, second);

        public override string ToString() => property.ToString();
    }

	/// <summary>
	/// Provides typed access to static property.
	/// </summary>
	/// <typeparam name="V">Type of property.</typeparam>
	public sealed class Property<V> : PropertyBase<V>, IProperty<V>
	{
		private const BindingFlags PublicFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy;
		private const BindingFlags NonPublicFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

		private Property(PropertyInfo property)
			: base(property)
		{
			GetMethod = property.GetMethod?.Unreflect<MemberGetter<V>>();
			SetMethod = property.SetMethod?.Unreflect<MemberSetter<V>>();
		}

		public static implicit operator MemberGetter<V>(Property<V> property) => property?.GetMethod;
		public static implicit operator MemberSetter<V>(Property<V> property) => property?.SetMethod;

		/// <summary>
		/// Gets property getter.
		/// </summary>
		public new Method<MemberGetter<V>> GetMethod { get; }

		/// <summary>
		/// Gets property setter.
		/// </summary>
		public new Method<MemberSetter<V>> SetMethod { get; }

		/// <summary>
		/// Gets or sets property value.
		/// </summary>
		public V Value
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => GetMethod is null ? throw new InvalidOperationException(ExceptionMessages.PropertyWithoutGetter(Name)) : GetMethod.Invoke();
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set
			{
                if (SetMethod is null)
                    throw new InvalidOperationException(ExceptionMessages.PropertyWithoutSetter(Name));
                else
                    SetMethod.Invoke(value);
			}
		}

		public override bool GetValue(object obj, out V value)
		{
			if (GetMethod is null || !(obj is null))
			{
				value = default;
				return false;
			}
			else
			{
				value = GetMethod.Invoke();
				return true;
			}
		}

		public override bool SetValue(object obj, V value)
		{
			if (SetMethod is null || !(obj is null))
				return false;
			else
			{
				SetMethod.Invoke(value);
				return true;
			}
		}

		internal static Property<V> Reflect<T>(string propertyName, bool nonPublic)
		{
			var property = typeof(T).GetProperty(propertyName, (nonPublic ? NonPublicFlags : PublicFlags));
			return property.PropertyType == typeof(V) && property.GetIndexParameters().IsNullOrEmpty() ?
				new Property<V>(property) :
				null;
		}
	}

	/// <summary>
	/// Provides typed access to instance property declared in type <typeparamref name="T"/>.
	/// </summary>
	/// <typeparam name="T">Declaring type.</typeparam>
	/// <typeparam name="V">Type of property.</typeparam>
	public sealed class Property<T, V> : PropertyBase<V>, IProperty<T, V>
	{
		private const BindingFlags PublicFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy;
		private const BindingFlags NonPublicFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;


		private Property(PropertyInfo property)
			: base(property)
		{
            GetMethod = property.GetMethod?.Unreflect<MemberGetter<T, V>>();
            SetMethod = property.SetMethod?.Unreflect<MemberSetter<T, V>>();
        }

		public static implicit operator MemberGetter<T, V>(Property<T, V> property)
			=> property?.GetMethod;
		public static implicit operator MemberSetter<T, V>(Property<T, V> property)
			=> property?.SetMethod;

		/// <summary>
		/// Gets property getter.
		/// </summary>
		public new Method<MemberGetter<T, V>> GetMethod { get; }

		/// <summary>
		/// Gets property setter.
		/// </summary>
		public new Method<MemberSetter<T, V>> SetMethod { get; }

		public override bool GetValue(object obj, out V value)
		{
			if(GetMethod is null || !(obj is T))
			{
				value = default;
				return false;
			}
			else
			{
				value = GetMethod.Invoke((T)obj);
				return true;
			}
		}

		public override bool SetValue(object obj, V value)
		{
			if (SetMethod is null || !(obj is T))
				return false;
			else
			{
				SetMethod.Invoke((T)obj, value);
				return true;
			}
		}

		/// <summary>
		/// Gets or sets property value.
		/// </summary>
		/// <param name="owner">Property instance.</param>
		/// <returns>Property value.</returns>
		public V this[in T owner]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => GetMethod is null ? throw new InvalidOperationException(ExceptionMessages.PropertyWithoutGetter(Name)) : GetMethod.Invoke(owner);
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set
			{
				if (SetMethod is null)
					throw new InvalidOperationException(ExceptionMessages.PropertyWithoutSetter(Name));
				else
                    SetMethod.Invoke(owner, value);
			}
		}

		internal static Property<T, V> Reflect(string propertyName, bool nonPublic)
		{
			var property = typeof(T).GetProperty(propertyName, (nonPublic ? NonPublicFlags : PublicFlags));
			return property.PropertyType == typeof(V) && property.GetIndexParameters().IsNullOrEmpty() ?
				new Property<T, V>(property) :
				null;
		}
	}
}