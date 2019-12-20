using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using static System.Linq.Expressions.Expression;

namespace DotNext.Reflection
{
    /// <summary>
    /// Represents reflected field.
    /// </summary>
    /// <typeparam name="V">Type of field value.</typeparam>
    public abstract class FieldBase<V> : FieldInfo, IField, IEquatable<FieldInfo>
    {
        private readonly FieldInfo field;

        private protected FieldBase(FieldInfo field)
        {
            this.field = field;
        }

        /// <summary>
        /// Returns the value of a field supported by a given object.
        /// </summary>
        /// <param name="obj">The object whose field value will be returned.</param>
        /// <param name="value">An object containing the value of the field reflected by this instance.</param>
        /// <returns><see langword="true"/>, if field value is obtained successfully; otherwise, <see langword="false"/>.</returns>
        public abstract bool GetValue(object obj, out V value);

        /// <summary>
        /// Sets the value of the field supported by the given object.
        /// </summary>
        /// <param name="obj">The object whose field value will be set.</param>
        /// <param name="value">The value to assign to the field.</param>
        /// <returns><see langword="true"/>, if field value is modified successfully; otherwise, <see langword="false"/>.</returns>
        public abstract bool SetValue(object obj, V value);

        /// <summary>
        /// Gets the class that declares this field.
        /// </summary>
        public sealed override Type DeclaringType => field.DeclaringType;

        /// <summary>
        /// Always returns <see cref="MemberTypes.Field"/>.
        /// </summary>
        public sealed override MemberTypes MemberType => field.MemberType;

        /// <summary>
        /// Gets name of the field.
        /// </summary>
        public sealed override string Name => field.Name;

        /// <summary>
        /// Gets the class object that was used to obtain this instance.
        /// </summary>
        public sealed override Type ReflectedType => field.ReflectedType;

        /// <summary>
        /// Returns an array of all custom attributes applied to this field.
        /// </summary>
        /// <param name="inherit"><see langword="true"/> to search this member's inheritance chain to find the attributes; otherwise, <see langword="false"/>.</param>
        /// <returns>An array that contains all the custom attributes applied to this field.</returns>
        public sealed override object[] GetCustomAttributes(bool inherit) => field.GetCustomAttributes(inherit);

        /// <summary>
        /// Returns an array of all custom attributes applied to this field.
        /// </summary>
        /// <param name="attributeType">The type of attribute to search for. Only attributes that are assignable to this type are returned.</param>
        /// <param name="inherit"><see langword="true"/> to search this member's inheritance chain to find the attributes; otherwise, <see langword="false"/>.</param>
        /// <returns>An array that contains all the custom attributes applied to this field.</returns>
        public sealed override object[] GetCustomAttributes(Type attributeType, bool inherit) => field.GetCustomAttributes(attributeType, inherit);

        /// <summary>
        /// Determines whether one or more attributes of the specified type or of its derived types is applied to this field.
        /// </summary>
        /// <param name="attributeType">The type of custom attribute to search for. The search includes derived types.</param>
        /// <param name="inherit"><see langword="true"/> to search this member's inheritance chain to find the attributes; otherwise, <see langword="false"/>.</param>
        /// <returns><see langword="true"/> if one or more instances of <paramref name="attributeType"/> or any of its derived types is applied to this field; otherwise, <see langword="false"/>.</returns>
        public sealed override bool IsDefined(Type attributeType, bool inherit) => field.IsDefined(attributeType, inherit);

        /// <summary>
        /// Gets a value that identifies a metadata element.
        /// </summary>
        public sealed override int MetadataToken => field.MetadataToken;

        /// <summary>
        /// Gets the module in which the type that declares the field represented by the current instance is defined.
        /// </summary>
        public sealed override Module Module => field.Module;

        /// <summary>
        /// Gets a collection that contains this member's custom attributes.
        /// </summary>
        public sealed override IList<CustomAttributeData> GetCustomAttributesData() => field.GetCustomAttributesData();

        /// <summary>
        /// Gets a collection that contains this member's custom attributes.
        /// </summary>
        public sealed override IEnumerable<CustomAttributeData> CustomAttributes => field.CustomAttributes;

        /// <summary>
        /// Gets the attributes associated with this field.
        /// </summary>
        public sealed override FieldAttributes Attributes => field.Attributes;

        /// <summary>
        /// Gets a handle to the internal metadata representation of this field.
        /// </summary>
        public sealed override RuntimeFieldHandle FieldHandle => field.FieldHandle;

        /// <summary>
        /// Gets type of this field.
        /// </summary>
        public sealed override Type FieldType => field.FieldType;

        /// <summary>
        /// Gets an array of types that identify the optional custom modifiers of the field.
        /// </summary>
        /// <returns>An array of objects that identify the optional custom modifiers of the current field.</returns>
        public sealed override Type[] GetOptionalCustomModifiers() => field.GetOptionalCustomModifiers();

        /// <summary>
        /// Returns a literal value associated with the field by a compiler.
        /// </summary>
        /// <returns>The literal value associated with the field. If the literal value is a class type with an element value of zero, 
        /// the return value is <see langword="null"/>.</returns>
        public sealed override object GetRawConstantValue() => field.GetRawConstantValue();

        /// <summary>
        /// Gets an array of types that identify the required custom modifiers of the field.
        /// </summary>
        /// <returns>An array of objects that identify the required custom modifiers of the current field.</returns>
        public sealed override Type[] GetRequiredCustomModifiers() => field.GetRequiredCustomModifiers();

        /// <summary>
        /// Returns the value of a field supported by a given object.
        /// </summary>
        /// <param name="obj">The object whose field value will be returned.</param>
        /// <returns>An object containing the value of the field reflected by this instance.</returns>
        public override object GetValue(object obj) => field.GetValue(obj);

        /// <summary>
        /// Returns the value of a field supported by a given object.
        /// </summary>
        /// <param name="obj">A managed pointer to a location and a runtime representation of the type that might be stored at that location.</param>
        /// <returns>An object containing the value of the field reflected by this instance.</returns>
        [CLSCompliant(false)]
        public sealed override object GetValueDirect(TypedReference obj) => field.GetValueDirect(obj);

        /// <summary>
        /// Gets a value that indicates whether the field is security-critical or security-safe-critical at the current trust level, 
        /// and therefore can perform critical operations.
        /// </summary>
        public sealed override bool IsSecurityCritical => field.IsSecurityCritical;

        /// <summary>
        /// Gets a value that indicates whether the field is security-safe-critical at the current trust level; that is, 
        /// whether it can perform critical operations and can be accessed by transparent code.
        /// </summary>
        public sealed override bool IsSecuritySafeCritical => field.IsSecuritySafeCritical;

        /// <summary>
        /// Gets a value that indicates whether the current field is transparent at the current trust level, 
        /// and therefore cannot perform critical operations.
        /// </summary>
        public sealed override bool IsSecurityTransparent => field.IsSecurityTransparent;

        /// <summary>
        /// Sets the value of the field supported by the given object.
        /// </summary>
        /// <param name="obj">The object whose field value will be returned.</param>
        /// <param name="value">The value to assign to the field.</param>
        /// <param name="invokeAttr">The type of binding that is desired.</param>
        /// <param name="binder">A set of properties that enables the binding, coercion of argument types, and invocation of members through reflection.</param>
        /// <param name="culture">The software preferences of a particular culture.</param>
        public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, CultureInfo culture)
            => field.SetValue(obj, value, invokeAttr, binder, culture);

        /// <summary>
        /// Sets the value of the field supported by the given object.
        /// </summary>
        /// <param name="obj">A managed pointer to a location and a runtime representation of the type that might be stored at that location.</param>
        /// <param name="value">The value to assign to the field.</param>
        [CLSCompliant(false)]
        public sealed override void SetValueDirect(TypedReference obj, object value)
            => field.SetValueDirect(obj, value);

        /// <summary>
        /// Determines whether this field is read-only and cannot be modified.
        /// </summary>
        public bool IsReadOnly => field.Attributes.HasFlag(FieldAttributes.InitOnly);

        FieldInfo IMember<FieldInfo>.RuntimeMember => field;

        /// <summary>
        /// Determines whether this field is equal to the given field.
        /// </summary>
        /// <param name="other">Other field to compare.</param>
        /// <returns><see langword="true"/> if this object reflects the same field as the specified object; otherwise, <see langword="false"/>.</returns>
        public bool Equals(FieldInfo other) => other is FieldBase<V> field ? field.field == this.field : this.field == other;

        /// <summary>
        /// Computes hash code uniquely identifies the reflected field.
        /// </summary>
        /// <returns>The hash code of the field.</returns>
        public sealed override int GetHashCode() => field.GetHashCode();

        /// <summary>
        /// Determines whether this field is equal to the given field.
        /// </summary>
        /// <param name="other">Other field to compare.</param>
        /// <returns><see langword="true"/> if this object reflects the same field as the specified object; otherwise, <see langword="false"/>.</returns>
        public sealed override bool Equals(object other)
        {
            switch (other)
            {
                case FieldBase<V> field:
                    return this.field == field.field;
                case FieldInfo field:
                    return this.field == field;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns textual representation of this field.
        /// </summary>
        /// <returns>The textual representation of this field.</returns>
        public sealed override string ToString() => field.ToString();
    }

    /// <summary>
    /// Provides typed access to instance field declared in type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Declaring type.</typeparam>
    /// <typeparam name="V">Type of field value.</typeparam>
    public sealed class Field<T, V> : FieldBase<V>, IField<T, V>
    {
        private delegate ref V Provider(in T instance);

        private sealed class Cache : MemberCache<FieldInfo, Field<T, V>>
        {
            private protected override Field<T, V> Create(string fieldName, bool nonPublic) => Reflect(fieldName, nonPublic);
        }

        private static readonly UserDataSlot<Field<T, V>> CacheSlot = UserDataSlot<Field<T, V>>.Allocate();
        private const BindingFlags PubicFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy;
        private const BindingFlags NonPublicFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;


        private readonly MemberGetter<T, V> getter;
        private readonly MemberSetter<T, V> setter;
        private readonly Provider provider;

        private Field(FieldInfo field)
            : base(field)
        {
            if (field.DeclaringType is null)
                throw new ArgumentException(ExceptionMessages.ModuleMemberDetected(field), nameof(field));
            var instanceParam = Parameter(typeof(T).MakeByRefType());
            //TODO: Should be optimized when LINQ Expression will have a support for ref return
            provider = Lambda<Provider>(Call(typeof(Unsafe), nameof(Unsafe.AsRef), new[] { field.FieldType }, Field(instanceParam, field)), instanceParam).Compile();
            const BindingFlags staticPrivate = BindingFlags.Static | BindingFlags.DeclaredOnly | BindingFlags.NonPublic;
            getter = GetType().GetMethod(nameof(GetValue), staticPrivate).CreateDelegate<MemberGetter<T, V>>(provider);
            setter = field.IsInitOnly ? null : GetType().GetMethod(nameof(SetValue), staticPrivate).CreateDelegate<MemberSetter<T, V>>(provider);
        }

        private static V GetValue(Provider provider, in T instance) => provider(instance);

        private static void SetValue(Provider provider, in T instance, V value) => provider(instance) = value;

        /// <summary>
        /// Obtains field getter in the form of the delegate instance.
        /// </summary>
        /// <param name="field">The reflected field.</param>
        public static implicit operator MemberGetter<T, V>(Field<T, V> field) => field?.getter;

        /// <summary>
        /// Obtains field setter in the form of the delegate instance.
        /// </summary>
        /// <param name="field">The reflected field.</param>
        public static implicit operator MemberSetter<T, V>(Field<T, V> field) => field?.setter;

        /// <summary>
        /// Returns the value of a field supported by a given object.
        /// </summary>
        /// <param name="obj">The object whose field value will be returned.</param>
        /// <param name="value">An object containing the value of the field reflected by this instance.</param>
        /// <returns><see langword="true"/>, if field value is obtained successfully; otherwise, <see langword="false"/>.</returns>
        public override bool GetValue(object obj, out V value)
        {
            if (obj is T instance)
            {
                value = this[instance];
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        /// <summary>
        /// Sets the value of the field supported by the given object.
        /// </summary>
        /// <param name="obj">The object whose field value will be set.</param>
        /// <param name="value">The value to assign to the field.</param>
        /// <returns><see langword="true"/>, if field value is modified successfully; otherwise, <see langword="false"/>.</returns>
        public override bool SetValue(object obj, V value)
        {
            if (setter is null)
                return false;
            else if (obj is T instance)
            {
                this[instance] = value;
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Returns the value of a field supported by a given object.
        /// </summary>
        /// <param name="obj">The object whose field value will be returned.</param>
        /// <returns>An object containing the value of the field reflected by this instance.</returns>
        public override object GetValue(object obj)
            => obj is T instance ? this[instance] : throw new ArgumentException(ExceptionMessages.ObjectOfTypeExpected(obj, typeof(T)));

        /// <summary>
        /// Sets the value of the field supported by the given object.
        /// </summary>
        /// <param name="obj">The object whose field value will be returned.</param>
        /// <param name="value">The value to assign to the field.</param>
        /// <param name="invokeAttr">The type of binding that is desired.</param>
        /// <param name="binder">A set of properties that enables the binding, coercion of argument types, and invocation of members through reflection.</param>
        /// <param name="culture">The software preferences of a particular culture.</param>
        public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, CultureInfo culture)
        {
            if (setter is null)
                throw new InvalidOperationException(ExceptionMessages.ReadOnlyField(Name));
            else if (!(obj is T))
                throw new ArgumentException(ExceptionMessages.ObjectOfTypeExpected(obj, typeof(T)));
            else if (value is null)
                this[(T)obj] = FieldType.IsValueType ? throw new ArgumentException(ExceptionMessages.NullFieldValue) : default(V);
            else if (!(value is V))
                throw new ArgumentException(ExceptionMessages.ObjectOfTypeExpected(value, typeof(V)));
            else
                this[(T)obj] = (V)value;
        }

        /// <summary>
        /// Gets or sets instance field value.
        /// </summary>
        /// <param name="this"><c>this</c> argument.</param>
        public ref V this[in T @this]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref provider(@this);
        }

        private static Field<T, V> Reflect(string fieldName, bool nonPublic)
        {
            var field = typeof(T).GetField(fieldName, nonPublic ? NonPublicFlags : PubicFlags);
            return field is null ? null : new Field<T, V>(field);
        }

        internal static Field<T, V> GetOrCreate(string fieldName, bool nonPublic)
            => Cache.Of<Cache>(typeof(T)).GetOrCreate(fieldName, nonPublic);

        private static Field<T, V> Unreflect(FieldInfo field)
            => field.IsStatic ? throw new ArgumentException(ExceptionMessages.InstanceFieldExpected, nameof(field)) : new Field<T, V>(field);

        internal static Field<T, V> GetOrCreate(FieldInfo field) => field.GetUserData().GetOrSet(CacheSlot, field, new ValueFunc<FieldInfo, Field<T, V>>(Unreflect));
    }

    /// <summary>
    /// Provides typed access to static field declared in type <typeparamref name="V"/>.
    /// </summary>
    /// <typeparam name="V">Type of field value.</typeparam>
    public sealed class Field<V> : FieldBase<V>, IField<V>
    {
        private delegate ref V Provider();

        private sealed class Cache<T> : MemberCache<FieldInfo, Field<V>>
        {
            private protected override Field<V> Create(string fieldName, bool nonPublic) => Reflect(typeof(T), fieldName, nonPublic);
        }

        private static readonly UserDataSlot<Field<V>> CacheSlot = UserDataSlot<Field<V>>.Allocate();
        private const BindingFlags PubicFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly;
        private const BindingFlags NonPublicFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private readonly MemberGetter<V> getter;
        private readonly MemberSetter<V> setter;
        private readonly Provider provider;

        private Field(FieldInfo field)
            : base(field)
        {
            //TODO: Should be optimized when LINQ Expression will have a support for ref return
            provider = Lambda<Provider>(Call(typeof(Unsafe), nameof(Unsafe.AsRef), new[] { field.FieldType }, Field(null, field))).Compile();
            const BindingFlags staticPrivate = BindingFlags.Static | BindingFlags.DeclaredOnly | BindingFlags.NonPublic;
            getter = GetType().GetMethod(nameof(GetValue), staticPrivate).CreateDelegate<MemberGetter<V>>(provider);
            setter = field.IsInitOnly ? null : GetType().GetMethod(nameof(SetValue), staticPrivate).CreateDelegate<MemberSetter<V>>(provider);
        }

        private static V GetValue(Provider provider) => provider();

        private static void SetValue(Provider provider, V value) => provider() = value;

        /// <summary>
        /// Obtains field getter in the form of the delegate instance.
        /// </summary>
        /// <param name="field">The reflected field.</param>
        public static implicit operator MemberGetter<V>(Field<V> field) => field?.getter;

        /// <summary>
        /// Obtains field setter in the form of the delegate instance.
        /// </summary>
        /// <param name="field">The reflected field.</param>
        public static implicit operator MemberSetter<V>(Field<V> field) => field?.setter;

        /// <summary>
        /// Returns the value of a field supported by a given object.
        /// </summary>
        /// <param name="obj">Must be <see langword="null"/>.</param>
        /// <param name="value">An object containing the value of the field reflected by this instance.</param>
        /// <returns><see langword="true"/>, if field value is obtained successfully; otherwise, <see langword="false"/>.</returns>
        public override bool GetValue(object obj, out V value)
        {
            if (obj is null)
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

        /// <summary>
        /// Sets the value of the field supported by the given object.
        /// </summary>
        /// <param name="obj">Must be <see langword="null"/>.</param>
        /// <param name="value">The value to assign to the field.</param>
        /// <returns><see langword="true"/>, if field value is modified successfully; otherwise, <see langword="false"/>.</returns>
        public override bool SetValue(object obj, V value)
        {
            if (IsInitOnly)
                return false;
            else if (obj is null)
            {
                Value = value;
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Returns the value of a field supported by a given object.
        /// </summary>
        /// <param name="obj">Must be <see langword="null"/>.</param>
        /// <returns>An object containing the value of the field reflected by this instance.</returns>
        public override object GetValue(object obj) => Value;

        /// <summary>
        /// Sets the value of the field supported by the given object.
        /// </summary>
        /// <param name="obj">Must be <see langword="null"/>.</param>
        /// <param name="value">The value to assign to the field.</param>
        /// <param name="invokeAttr">The type of binding that is desired.</param>
        /// <param name="binder">A set of properties that enables the binding, coercion of argument types, and invocation of members through reflection.</param>
        /// <param name="culture">The software preferences of a particular culture.</param>
        public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, CultureInfo culture)
        {
            if (IsInitOnly)
                throw new InvalidOperationException(ExceptionMessages.ReadOnlyField(Name));
            else if (value is null)
                Value = FieldType.IsValueType ? throw new ArgumentException(ExceptionMessages.NullFieldValue) : default(V);
            else if (!(value is V))
                throw new ArgumentException(ExceptionMessages.ObjectOfTypeExpected(value, typeof(V)));
            else
                Value = (V)value;
        }

        /// <summary>
        /// Obtains managed pointer to the static field.
        /// </summary>
        /// <value>The managed pointer to the static field.</value>
        public ref V Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref provider();
        }

        private static Field<V> Reflect(Type declaringType, string fieldName, bool nonPublic)
        {
            var field = declaringType.GetField(fieldName, nonPublic ? NonPublicFlags : PubicFlags);
            return field is null ? null : new Field<V>(field);
        }

        private static Field<V> Unreflect(FieldInfo field)
            => field.IsStatic ? new Field<V>(field) : throw new ArgumentException(ExceptionMessages.StaticFieldExpected, nameof(field));

        internal static Field<V> GetOrCreate(FieldInfo field) => field.GetUserData().GetOrSet(CacheSlot, field, new ValueFunc<FieldInfo, Field<V>>(Unreflect));

        internal static Field<V> GetOrCreate<T>(string fieldName, bool nonPublic)
            => Cache<T>.Of<Cache<T>>(typeof(T)).GetOrCreate(fieldName, nonPublic);
    }
}