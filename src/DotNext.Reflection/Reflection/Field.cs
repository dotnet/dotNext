using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using static System.Linq.Expressions.Expression;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using static InlineIL.MethodRef;
using static InlineIL.TypeRef;

namespace DotNext.Reflection
{
    using ReflectionUtils = Runtime.CompilerServices.ReflectionUtils;

    /// <summary>
    /// Represents reflected field.
    /// </summary>
    /// <typeparam name="TValue">Type of field value.</typeparam>
    public abstract class FieldBase<TValue> : FieldInfo, IField, IEquatable<FieldInfo?>
    {
        private readonly FieldInfo field;

        private protected FieldBase(FieldInfo field) => this.field = field;

        private protected static bool IsVolatile(FieldInfo field)
        {
            var volatileModifier = typeof(IsVolatile);
            foreach (var modified in field.GetRequiredCustomModifiers())
            {
                if (modified == volatileModifier)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the value of a field supported by a given object.
        /// </summary>
        /// <param name="obj">The object whose field value will be returned.</param>
        /// <param name="value">An object containing the value of the field reflected by this instance.</param>
        /// <returns><see langword="true"/>, if field value is obtained successfully; otherwise, <see langword="false"/>.</returns>
        public abstract bool GetValue(object? obj, out TValue? value);

        /// <summary>
        /// Sets the value of the field supported by the given object.
        /// </summary>
        /// <param name="obj">The object whose field value will be set.</param>
        /// <param name="value">The value to assign to the field.</param>
        /// <returns><see langword="true"/>, if field value is modified successfully; otherwise, <see langword="false"/>.</returns>
        public abstract bool SetValue(object? obj, TValue value);

        /// <summary>
        /// Gets the class that declares this field.
        /// </summary>
        public sealed override Type? DeclaringType => field.DeclaringType;

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
        public sealed override Type? ReflectedType => field.ReflectedType;

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

        /// <inheritdoc />
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
        public sealed override object? GetRawConstantValue() => field.GetRawConstantValue();

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
        public override object? GetValue(object? obj) => field.GetValue(obj);

        /// <summary>
        /// Returns the value of a field supported by a given object.
        /// </summary>
        /// <param name="obj">A managed pointer to a location and a runtime representation of the type that might be stored at that location.</param>
        /// <returns>An object containing the value of the field reflected by this instance.</returns>
        [CLSCompliant(false)]
        public sealed override object? GetValueDirect(TypedReference obj) => field.GetValueDirect(obj);

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
        public override void SetValue(object? obj, object? value, BindingFlags invokeAttr, Binder? binder, CultureInfo? culture)
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
        public bool IsReadOnly => (field.Attributes & FieldAttributes.InitOnly) != 0;

        /// <inheritdoc/>
        FieldInfo IMember<FieldInfo>.Metadata => field;

        /// <summary>
        /// Determines whether this field is equal to the given field.
        /// </summary>
        /// <param name="other">Other field to compare.</param>
        /// <returns><see langword="true"/> if this object reflects the same field as the specified object; otherwise, <see langword="false"/>.</returns>
        public bool Equals(FieldInfo? other) => other is FieldBase<TValue> field ? field.field == this.field : this.field == other;

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
        public sealed override bool Equals(object? other) => other switch
        {
            FieldBase<TValue> field => this.field == field.field,
            FieldInfo field => this.field == field,
            _ => false,
        };

        /// <summary>
        /// Returns textual representation of this field.
        /// </summary>
        /// <returns>The textual representation of this field.</returns>
        public sealed override string? ToString() => field.ToString();
    }

    /// <summary>
    /// Provides typed access to instance field declared in type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Declaring type.</typeparam>
    /// <typeparam name="TValue">Type of field value.</typeparam>
    public sealed class Field<T, TValue> : FieldBase<TValue>, IField<T, TValue>
    {
        private delegate ref TValue? Provider(in T instance);

        private sealed class Cache : MemberCache<FieldInfo, Field<T, TValue>>
        {
            private protected override Field<T, TValue>? Create(string fieldName, bool nonPublic) => Reflect(fieldName, nonPublic);
        }

        private const BindingFlags PubicFlags = BindingFlags.Instance | BindingFlags.Public;
        private const BindingFlags NonPublicFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static readonly UserDataSlot<Field<T, TValue>> CacheSlot = UserDataSlot<Field<T, TValue>>.Allocate();

        private readonly MemberGetter<T, TValue> getter;
        private readonly MemberSetter<T, TValue>? setter;
        private readonly Provider provider;

        private Field(FieldInfo field)
            : base(field)
        {
            if (field.DeclaringType is null)
                throw new ArgumentException(ExceptionMessages.ModuleMemberDetected(field), nameof(field));
            var instanceParam = Parameter(typeof(T).MakeByRefType());
            var isVolatile = IsVolatile(field);

            // TODO: Should be optimized when LINQ Expression will have a support for ref return
            provider = Lambda<Provider>(Call(typeof(Unsafe), nameof(Unsafe.AsRef), new[] { field.FieldType }, Field(instanceParam, field)), instanceParam).Compile();

            Push(provider);
            if (isVolatile)
                Ldftn(Method(Type<Field<T, TValue>>(), nameof(GetValueVolatile), Type<Provider>(), Type<T>().MakeByRefType()));
            else
                Ldftn(Method(Type<Field<T, TValue>>(), nameof(GetValue), Type<Provider>(), Type<T>().MakeByRefType()));
            Newobj(Constructor(Type<MemberGetter<T, TValue>>(), Type<object>(), Type<IntPtr>()));
            Pop(out MemberGetter<T, TValue> getter);
            this.getter = getter;

            if (field.IsInitOnly)
            {
                setter = null;
            }
            else
            {
                Push(provider);
                if (isVolatile)
                    Ldftn(Method(Type<Field<T, TValue>>(), nameof(SetValueVolatile), Type<Provider>(), Type<T>().MakeByRefType(), Type<TValue>()));
                else
                    Ldftn(Method(Type<Field<T, TValue>>(), nameof(SetValue), Type<Provider>(), Type<T>().MakeByRefType(), Type<TValue>()));
                Newobj(Constructor(Type<MemberSetter<T, TValue>>(), Type<object>(), Type<IntPtr>()));
                Pop(out MemberSetter<T, TValue> setter);
                this.setter = setter;
            }
        }

        private static TValue? GetValue(Provider provider, [DisallowNull] in T instance) => provider(instance);

        private static TValue? GetValueVolatile(Provider provider, [DisallowNull] in T instance)
            => ReflectionUtils.VolatileRead(ref provider(instance));

        private static void SetValue(Provider provider, [DisallowNull] in T instance, TValue value) => provider(instance) = value;

        private static void SetValueVolatile(Provider provider, [DisallowNull] in T instance, TValue value)
            => ReflectionUtils.VolatileWrite(ref provider(instance), value);

        /// <summary>
        /// Obtains field getter in the form of the delegate instance.
        /// </summary>
        /// <param name="field">The reflected field.</param>
        [return: NotNullIfNotNull("field")]
        public static implicit operator MemberGetter<T, TValue>?(Field<T, TValue>? field) => field?.getter;

        /// <summary>
        /// Obtains field setter in the form of the delegate instance.
        /// </summary>
        /// <param name="field">The reflected field.</param>
        public static implicit operator MemberSetter<T, TValue>?(Field<T, TValue>? field) => field?.setter;

        /// <summary>
        /// Returns the value of a field supported by a given object.
        /// </summary>
        /// <param name="obj">The object whose field value will be returned.</param>
        /// <param name="value">An object containing the value of the field reflected by this instance.</param>
        /// <returns><see langword="true"/>, if field value is obtained successfully; otherwise, <see langword="false"/>.</returns>
        public override bool GetValue(object? obj, out TValue? value)
        {
            if (obj is T instance)
            {
                value = getter(in instance);
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Sets the value of the field supported by the given object.
        /// </summary>
        /// <param name="obj">The object whose field value will be set.</param>
        /// <param name="value">The value to assign to the field.</param>
        /// <returns><see langword="true"/>, if field value is modified successfully; otherwise, <see langword="false"/>.</returns>
        public override bool SetValue(object? obj, TValue value)
        {
            if (setter is null)
                return false;
            if (obj is T instance)
            {
                setter(in instance, value);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the value of a field supported by a given object.
        /// </summary>
        /// <param name="obj">The object whose field value will be returned.</param>
        /// <returns>An object containing the value of the field reflected by this instance.</returns>
        public override object? GetValue(object? obj)
            => obj is T instance ? getter(in instance) : throw new ArgumentException(ExceptionMessages.ObjectOfTypeExpected(obj, typeof(T)));

        /// <summary>
        /// Sets the value of the field supported by the given object.
        /// </summary>
        /// <param name="obj">The object whose field value will be returned.</param>
        /// <param name="value">The value to assign to the field.</param>
        /// <param name="invokeAttr">The type of binding that is desired.</param>
        /// <param name="binder">A set of properties that enables the binding, coercion of argument types, and invocation of members through reflection.</param>
        /// <param name="culture">The software preferences of a particular culture.</param>
        public override void SetValue(object? obj, object? value, BindingFlags invokeAttr, Binder? binder, CultureInfo? culture)
        {
            if (setter is null)
                throw new InvalidOperationException(ExceptionMessages.ReadOnlyField(Name));
            if (obj is not T instance)
                throw new ArgumentException(ExceptionMessages.ObjectOfTypeExpected(obj, typeof(T)));
            if (value is null)
                setter(in instance, FieldType.IsValueType ? throw new ArgumentException(ExceptionMessages.NullFieldValue) : default(TValue)!);
            if (value is not TValue typedValue)
                throw new ArgumentException(ExceptionMessages.ObjectOfTypeExpected(value, typeof(TValue)));
            setter(in instance, typedValue);
        }

        /// <summary>
        /// Gets or sets instance field value.
        /// </summary>
        /// <remarks>
        /// If the underlying field is volatile then you need to use static methods from <see cref="Volatile"/> explicitly to
        /// implement volatile semantics.
        /// </remarks>
        /// <param name="this"><c>this</c> argument.</param>
        public ref TValue? this[[DisallowNull] in T @this]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref provider(@this);
        }

        private static Field<T, TValue>? Reflect(string fieldName, bool nonPublic)
        {
            FieldInfo? field = typeof(T).GetField(fieldName, nonPublic ? NonPublicFlags : PubicFlags);
            return field is null ? null : new Field<T, TValue>(field);
        }

        internal static Field<T, TValue>? GetOrCreate(string fieldName, bool nonPublic)
            => Cache.Of<Cache>(typeof(T)).GetOrCreate(fieldName, nonPublic);

        private static Field<T, TValue> Unreflect(FieldInfo field)
            => field.IsStatic ? throw new ArgumentException(ExceptionMessages.InstanceFieldExpected, nameof(field)) : new Field<T, TValue>(field);

        internal static unsafe Field<T, TValue> GetOrCreate(FieldInfo field) => field.GetUserData().GetOrSet(CacheSlot, field, &Unreflect);
    }

    /// <summary>
    /// Provides typed access to static field declared in type <typeparamref name="TValue"/>.
    /// </summary>
    /// <typeparam name="TValue">Type of field value.</typeparam>
    public sealed class Field<TValue> : FieldBase<TValue>, IField<TValue>
    {
        private delegate ref TValue? Provider();

        private sealed class Cache<T> : MemberCache<FieldInfo, Field<TValue>>
        {
            private protected override Field<TValue>? Create(string fieldName, bool nonPublic) => Reflect(typeof(T), fieldName, nonPublic);
        }

        private const BindingFlags PubicFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly;
        private const BindingFlags NonPublicFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static readonly UserDataSlot<Field<TValue>> CacheSlot = UserDataSlot<Field<TValue>>.Allocate();

        private readonly MemberGetter<TValue> getter;
        private readonly MemberSetter<TValue>? setter;
        private readonly Provider provider;

        private Field(FieldInfo field)
            : base(field)
        {
            // TODO: Should be optimized when LINQ Expression will have a support for ref return
            provider = Lambda<Provider>(Call(typeof(Unsafe), nameof(Unsafe.AsRef), new[] { field.FieldType }, Field(null, field))).Compile();
            var isVolatile = IsVolatile(field);

            Push(provider);
            if (isVolatile)
                Ldftn(Method(Type<Field<TValue>>(), nameof(GetValueVolatile), Type<Provider>()));
            else
                Ldftn(Method(Type<Field<TValue>>(), nameof(GetValue), Type<Provider>()));
            Newobj(Constructor(Type<MemberGetter<TValue>>(), Type<object>(), Type<IntPtr>()));
            Pop(out MemberGetter<TValue> getter);
            this.getter = getter;

            if (field.IsInitOnly)
            {
                setter = null;
            }
            else
            {
                Push(provider);
                if (isVolatile)
                    Ldftn(Method(Type<Field<TValue>>(), nameof(SetValueVolatile), Type<Provider>(), Type<TValue>()));
                else
                    Ldftn(Method(Type<Field<TValue>>(), nameof(SetValue), Type<Provider>(), Type<TValue>()));
                Newobj(Constructor(Type<MemberSetter<TValue>>(), Type<object>(), Type<IntPtr>()));
                Pop(out MemberSetter<TValue> setter);
                this.setter = setter;
            }
        }

        private static TValue? GetValue(Provider provider) => provider();

        private static TValue? GetValueVolatile(Provider provider) => ReflectionUtils.VolatileRead(ref provider());

        private static void SetValue(Provider provider, TValue value) => provider() = value;

        private static void SetValueVolatile(Provider provider, TValue value) => ReflectionUtils.VolatileWrite(ref provider(), value);

        /// <summary>
        /// Obtains field getter in the form of the delegate instance.
        /// </summary>
        /// <param name="field">The reflected field.</param>
        [return: NotNullIfNotNull("field")]
        public static implicit operator MemberGetter<TValue>?(Field<TValue>? field) => field?.getter;

        /// <summary>
        /// Obtains field setter in the form of the delegate instance.
        /// </summary>
        /// <param name="field">The reflected field.</param>
        public static implicit operator MemberSetter<TValue>?(Field<TValue>? field) => field?.setter;

        /// <summary>
        /// Returns the value of a field supported by a given object.
        /// </summary>
        /// <param name="obj">Must be <see langword="null"/>.</param>
        /// <param name="value">An object containing the value of the field reflected by this instance.</param>
        /// <returns><see langword="true"/>, if field value is obtained successfully; otherwise, <see langword="false"/>.</returns>
        public override bool GetValue(object? obj, out TValue? value)
        {
            if (obj is null)
            {
                value = getter();
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Sets the value of the field supported by the given object.
        /// </summary>
        /// <param name="obj">Must be <see langword="null"/>.</param>
        /// <param name="value">The value to assign to the field.</param>
        /// <returns><see langword="true"/>, if field value is modified successfully; otherwise, <see langword="false"/>.</returns>
        public override bool SetValue(object? obj, TValue value)
        {
            if (setter is null)
                return false;
            if (obj is null)
            {
                setter(value);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the value of a field supported by a given object.
        /// </summary>
        /// <param name="obj">Must be <see langword="null"/>.</param>
        /// <returns>An object containing the value of the field reflected by this instance.</returns>
        public override object? GetValue(object? obj) => getter();

        /// <summary>
        /// Sets the value of the field supported by the given object.
        /// </summary>
        /// <param name="obj">Must be <see langword="null"/>.</param>
        /// <param name="value">The value to assign to the field.</param>
        /// <param name="invokeAttr">The type of binding that is desired.</param>
        /// <param name="binder">A set of properties that enables the binding, coercion of argument types, and invocation of members through reflection.</param>
        /// <param name="culture">The software preferences of a particular culture.</param>
        public override void SetValue(object? obj, object? value, BindingFlags invokeAttr, Binder? binder, CultureInfo? culture)
        {
            if (setter is null)
                throw new InvalidOperationException(ExceptionMessages.ReadOnlyField(Name));
            if (value is null)
                setter(FieldType.IsValueType ? throw new ArgumentException(ExceptionMessages.NullFieldValue) : default(TValue)!);
            if (value is not TValue typedValue)
                throw new ArgumentException(ExceptionMessages.ObjectOfTypeExpected(value, typeof(TValue)));
            setter(typedValue);
        }

        /// <summary>
        /// Obtains managed pointer to the static field.
        /// </summary>
        /// <remarks>
        /// If the underlying field is volatile then you need to use static methods from <see cref="Volatile"/> explicitly to
        /// implement volatile semantics.
        /// </remarks>
        /// <value>The managed pointer to the static field.</value>
        public ref TValue? Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref provider()!;
        }

        private static Field<TValue>? Reflect(Type declaringType, string fieldName, bool nonPublic)
        {
            FieldInfo? field = declaringType.GetField(fieldName, nonPublic ? NonPublicFlags : PubicFlags);
            return field is null ? null : new Field<TValue>(field);
        }

        private static Field<TValue> Unreflect(FieldInfo field)
            => field.IsStatic ? new Field<TValue>(field) : throw new ArgumentException(ExceptionMessages.StaticFieldExpected, nameof(field));

        internal static unsafe Field<TValue> GetOrCreate(FieldInfo field) => field.GetUserData().GetOrSet(CacheSlot, field, &Unreflect);

        internal static Field<TValue>? GetOrCreate<T>(string fieldName, bool nonPublic)
            => Cache<T>.Of<Cache<T>>(typeof(T)).GetOrCreate(fieldName, nonPublic);
    }
}