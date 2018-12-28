using System;
using System.Globalization;
using System.Collections.Generic;
using System.Reflection;
using static System.Linq.Expressions.Expression;
using System.Runtime.CompilerServices;

namespace Cheats.Reflection
{
    /// <summary>
    /// Represents reflected field.
    /// </summary>
    /// <typeparam name="V">Type of field value.</typeparam>
    public abstract class FieldBase<V> : FieldInfo, IField, IEquatable<FieldBase<V>>, IEquatable<FieldInfo>
    {
        private readonly FieldInfo field;

        private protected FieldBase(FieldInfo field)
        {
            this.field = field;
        }

        public abstract bool GetValue(object obj, out V value);
        public abstract bool SetValue(object obj, V value);

        public sealed override Type DeclaringType => field.DeclaringType;

        public sealed override MemberTypes MemberType => field.MemberType;

        public sealed override string Name => field.Name;

        public sealed override Type ReflectedType => field.ReflectedType;

        public sealed override object[] GetCustomAttributes(bool inherit) => field.GetCustomAttributes(inherit);
        public sealed override object[] GetCustomAttributes(Type attributeType, bool inherit) => field.GetCustomAttributes(attributeType, inherit);

        public sealed override bool IsDefined(Type attributeType, bool inherit) => field.IsDefined(attributeType, inherit);

        public sealed override int MetadataToken => field.MetadataToken;

        public sealed override Module Module => field.Module;

        public sealed override IList<CustomAttributeData> GetCustomAttributesData() => field.GetCustomAttributesData();

        public sealed override IEnumerable<CustomAttributeData> CustomAttributes => field.CustomAttributes;

        public sealed override FieldAttributes Attributes => field.Attributes;

        public sealed override RuntimeFieldHandle FieldHandle => field.FieldHandle;

        public sealed override Type FieldType => field.FieldType;

        public sealed override Type[] GetOptionalCustomModifiers() => field.GetOptionalCustomModifiers();

        public sealed override object GetRawConstantValue() => field.GetRawConstantValue();

        public sealed override Type[] GetRequiredCustomModifiers() => field.GetRequiredCustomModifiers();

        public override object GetValue(object obj) => field.GetValue(obj);

        [CLSCompliant(false)]
        public sealed override object GetValueDirect(TypedReference obj) => field.GetValueDirect(obj);

        public sealed override bool IsSecurityCritical => field.IsSecurityCritical;

        public sealed override bool IsSecuritySafeCritical => field.IsSecuritySafeCritical;

        public sealed override bool IsSecurityTransparent => field.IsSecurityTransparent;

        public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, CultureInfo culture)
            => field.SetValue(obj, value, invokeAttr, binder, culture);

        [CLSCompliant(false)]
        public sealed override void SetValueDirect(TypedReference obj, object value)
            => field.SetValueDirect(obj, value);

        public bool IsReadOnly => field.Attributes.HasFlag(FieldAttributes.InitOnly);

        FieldInfo IMember<FieldInfo>.RuntimeMember => field;

        public bool Equals(FieldInfo other) => field.Equals(other);

        public bool Equals(FieldBase<V> other) => other != null && Equals(other.field);

        public sealed override int GetHashCode() => field.GetHashCode();

        public sealed override bool Equals(object other)
        {
            switch (other)
            {
                case FieldBase<V> field:
                    return Equals(field);
                case FieldInfo field:
                    return Equals(field);
                default:
                    return false;
            }
        }

        public sealed override string ToString() => field.ToString();

        public static bool operator ==(FieldBase<V> first, FieldBase<V> second) => Equals(first, second);

        public static bool operator !=(FieldBase<V> first, FieldBase<V> second) => !Equals(first, second);
    }

    /// <summary>
    /// Provides typed access to instance field declared in type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Declaring type.</typeparam>
    /// <typeparam name="V">Type of field value.</typeparam>
    public sealed class Field<T, V> : Reflection.FieldBase<V>, IField<T, V>
    {
        private const BindingFlags PubicFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy;
        private const BindingFlags NonPublicFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        
        /// <summary>
        /// Represents field getter.
        /// </summary>
        /// <param name="this">This parameter.</param>
        /// <returns>Field value.</returns>
        public delegate V Getter(in T @this);

        /// <summary>
        /// Represents field setter.
        /// </summary>
        /// <param name="this">This parameter.</param>
        /// <param name="value">A value to set.</param>
        public delegate void Setter(in T @this, V value);

        private readonly Getter getter;
        private readonly Setter setter;

        private Field(FieldInfo field)
            : base(field)
        {
            var instanceParam = Parameter(field.DeclaringType.MakeByRefType());
            var valueParam = Parameter(field.FieldType);
            getter = Lambda<Getter>(Field(instanceParam, field), instanceParam).Compile();
            setter = field.IsInitOnly ? null : Lambda<Setter>(Assign(Field(instanceParam, field), valueParam), instanceParam, valueParam).Compile();
        }

        public static implicit operator Getter(Field<T, V> field) => field?.getter;
        public static implicit operator Setter(Field<T, V> field) => field?.setter;

        public override bool GetValue(object obj, out V value)
        {
            if(obj is T instance)
            {
                value = this[instance];
                return true;
            }
            else
            {
                value  = default;
                return false;
            }
        }
        public override bool SetValue(object obj, V value)
        {
            if(IsInitOnly)
                return false;
            else if(obj is T instance)
            {
                this[instance] = value;
                return true;
            }
            else
                return false;
        }

        public override object GetValue(object obj)
            => obj is T instance ? this[instance]: throw new ArgumentException($"Object {obj} must be of type {typeof(T)}");

        public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, CultureInfo culture)
        {
            if(IsInitOnly)
                new InvalidOperationException($"Field {Name} is read-only");
            else if(!(obj is T))
                throw new ArgumentException($"Object {obj} must be of type {typeof(T)}");
            else if(value is null)
                this[(T)obj] = FieldType.IsValueType ? throw new ArgumentException("Field value cannot be null") : default(V);
            else if(!(value is V))
                throw new ArgumentException($"Value {value} must be of type {typeof(V)}");
            else
                this[(T)obj] = (V)value;
        }

        /// <summary>
        /// Gets or sets instance field value.
        /// </summary>
        /// <param name="this">This parameter.</param>
        public V this[in T @this]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => getter(in @this);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if(setter is null)
                    new InvalidOperationException($"Field {Name} is read-only");
                else
                    setter(@this, value);
            }
        }

        internal static Field<T, V> Reflect(string fieldName, bool nonPublic)
        {
            var field = typeof(T).GetField(fieldName, nonPublic ? NonPublicFlags : PubicFlags);
            return field is null ? null : new Field<T, V>(field);
        }
    }

    /// <summary>
    /// Provides typed access to static field declared in type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="V">Type of field value.</typeparam>
    public sealed class Field<V> : Reflection.FieldBase<V>, IField<V>
    {
        private const BindingFlags PubicFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy;
        private const BindingFlags NonPublicFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private readonly Func<V> getter;
        private readonly Action<V> setter;

        private Field(FieldInfo field)
            : base(field)
        {
            var valueParam = Parameter(field.FieldType);
            getter = Lambda<Func<V>>(Field(null, field)).Compile();
            setter = field.IsInitOnly ? null : Lambda<Action<V>>(Assign(Field(null, field), valueParam), valueParam).Compile();
        }

        public static implicit operator Func<V>(Field<V> field) => field?.getter;
        public static implicit operator Action<V>(Field<V> field) => field?.setter;

        public override bool GetValue(object obj, out V value)
        {
            if(obj is null)
            {
                value = Value;
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }
        public override bool SetValue(object obj, V value)
        {
            if(IsInitOnly)
                return false;
            else if(obj is null)
            {
                Value = value;
                return true;
            }
            else
                return false;
        }

        public override object GetValue(object obj) => Value;

        public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, CultureInfo culture)
        {
            if(IsInitOnly)
                throw new InvalidOperationException($"Field {Name} is read-only");
            else if(value is null)
                Value = FieldType.IsValueType ? throw new ArgumentException("Field value cannot be null") : default(V);
            else if(!(value is V))
                throw new ArgumentException($"Value {value} must be of type {typeof(V)}");
            else
                Value = (V)value;
        }

        /// <summary>
        /// Gets or sets field value.
        /// </summary>s
        public V Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => getter();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if(setter is null)
                    throw new InvalidOperationException($"Field {Name} is read-only");
                else
                    setter(value);
            }
        }

        internal static Field<V> Reflect<T>(string fieldName, bool nonPublic)
        {
            var field = typeof(T).GetField(fieldName, nonPublic ? NonPublicFlags : PubicFlags);
            return field is null ? null : new Field<V>(field);
        }
    }
}