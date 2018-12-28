using System;
using System.Globalization;
using System.Collections.Generic;
using System.Reflection;

namespace DotNetCheats.Reflection
{
    /// <summary>
    /// Represents property.
    /// </summary>
    /// <typeparam name="P">Type of property value.</typeparam>
    public abstract class Property<P> : PropertyInfo, IProperty, IEquatable<Property<P>>, IEquatable<PropertyInfo>
    {
        private readonly PropertyInfo property;

        private protected Property(PropertyInfo property)
        {
            this.property = property;
        }

        public sealed override object GetValue(object obj, object[] index) => property.GetValue(obj, index);

        public sealed override void SetValue(object obj, object value, object[] index) => property.SetValue(obj, value, index);

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

        public bool Equals(Property<P> other)
            => other != null &&
                GetType() == other.GetType() &&
                property == other.property;

        public override bool Equals(object other)
        {
            switch (other)
            {
                case Property<P> property:
                    return Equals(property);
                case PropertyInfo property:
                    return Equals(property);
                default:
                    return false;
            }
        }

        public override int GetHashCode() => property.GetHashCode();

        public static bool operator ==(Property<P> first, Property<P> second) => Equals(first, second);

        public static bool operator !=(Property<P> first, Property<P> second) => !Equals(first, second);

        public override string ToString() => property.ToString();
    }

    // /// <summary>
    // /// Provides typed access to static property.
    // /// </summary>
    // /// <typeparam name="V">Type of property.</typeparam>
    // public sealed class StaticProperty<V> : Property<V>, IProperty<V>
    // {
    //     /// <summary>
    //     /// Represents static property getter.
    //     /// </summary>
    //     /// <returns>Property value.</returns>
    //     public delegate V Getter();

    //     /// <summary>
    //     /// Represents static property setter.
    //     /// </summary>
    //     /// <param name="value"></param>
    //     public delegate void Setter(V value);

    //     /// <summary>
    //     /// Represents property getter/setter.
    //     /// </summary>
    //     /// <param name="value">Property value to set or get.</param>
    //     /// <param name="action">An action to be performed on property.</param>
    //     /// <returns>True, if action is supported by member; otherwise, false.</returns>
    //     public delegate bool Accessor(ref V value, MemberAction action);

    //     private const BindingFlags PublicFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy;
    //     private const BindingFlags NonPublicFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        
        
    //     private StaticProperty(PropertyInfo property, bool nonPublic)
    //         : base(property)
    //     {
    //         var valueParam = Parameter(property.PropertyType.MakeByRefType());
    //         var actionParam = Parameter(typeof(MemberAction));

    //         var getter = property.GetGetMethod(nonPublic);
    //         var setter = property.GetSetMethod(nonPublic);

    //         if (getter is null) //write-only
    //             accessor = Lambda<MemberAccess<V>>(MemberAccess.GetOrSetValue(actionParam, null, Call(null, setter, valueParam)),
    //                 valueParam,
    //                 actionParam).Compile();
    //         else if (setter is null) //read-only
    //             accessor = Lambda<MemberAccess<V>>(MemberAccess.GetOrSetValue(actionParam, Assign(valueParam, Call(null, getter)), null),
    //                 valueParam,
    //                 actionParam).Compile();
    //         else //read-write
    //             accessor = Lambda<MemberAccess<V>>(MemberAccess.GetOrSetValue(actionParam, Assign(valueParam, Call(null, getter)), Call(null, setter, valueParam)),
    //                 valueParam,
    //                 actionParam).Compile();
    //     }

    //     public bool Invoke(ref V value, MemberAction action)
    //     {
    //         switch(action)
    //         {
    //             case MemberAction.GetValue:

    //         }
    //     }

    //     // public new Method<MemberAccess.Getter<P>> GetGetMethod(bool nonPublic)
    //     // {
    //     //     var getter = base.GetGetMethod(nonPublic);
    //     //     return getter == null ? null : StaticMethod<MemberAccess.Getter<P>>.Get(getter.Name, nonPublic);
    //     // }

    //     // public new Method<MemberAccess.Getter<P>> GetMethod
    //     // {
    //     //     get
    //     //     {
    //     //         var getter = base.GetMethod;
    //     //         return getter == null ? null : StaticMethod<MemberAccess.Getter<P>>.Get(getter.Name, !getter.IsPublic);
    //     //     }
    //     // }
    //     // public new Method<MemberAccess.Setter<P>> SetMethod
    //     // {
    //     //     get
    //     //     {
    //     //         var setter = base.SetMethod;
    //     //         return setter == null ? null : StaticMethod<MemberAccess.Setter<P>>.Get(setter.Name, !setter.IsPublic);
    //     //     }
    //     // }

    //     // public new Method<MemberAccess.Setter<P>> GetSetMethod(bool nonPublic)
    //     // {
    //     //     var setter = base.GetSetMethod(nonPublic);
    //     //     return setter == null ? null : StaticMethod<MemberAccess.Setter<P>>.Get(setter.Name, nonPublic);
    //     // }

    //     /// <summary>
    //     /// Gets or sets property value.
    //     /// </summary>
    //     public V Value
    //     {
    //         [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //         get => accessor.GetValue();
    //         [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //         set => accessor.SetValue(value);
    //     }

    //     public static implicit operator MemberAccess<V>(StaticProperty<V> property) => property?.accessor;
    // }
}