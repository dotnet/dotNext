using System;
using System.Globalization;
using System.Collections.Generic;
using System.Reflection;

namespace MissingPieces.Metaprogramming
{
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
}