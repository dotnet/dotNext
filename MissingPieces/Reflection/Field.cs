using System;
using System.Globalization;
using System.Collections.Generic;
using System.Reflection;

namespace MissingPieces.Reflection
{
    /// <summary>
    /// Represents reflected field.
    /// </summary>
    /// <typeparam name="T">Type of field value.</typeparam>
    public abstract class Field<T> : FieldInfo, IField, IEquatable<Field<T>>, IEquatable<FieldInfo>
    {
        private readonly FieldInfo field;

        private protected Field(FieldInfo field)
        {
            this.field = field;
        }

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

        public sealed override object GetValue(object obj) => field.GetValue(obj);

        [CLSCompliant(false)]
        public sealed override object GetValueDirect(TypedReference obj) => field.GetValueDirect(obj);

        public sealed override bool IsSecurityCritical => field.IsSecurityCritical;

        public sealed override bool IsSecuritySafeCritical => field.IsSecuritySafeCritical;

        public sealed override bool IsSecurityTransparent => field.IsSecurityTransparent;

        public sealed override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, CultureInfo culture)
            => field.SetValue(obj, value, invokeAttr, binder, culture);

        [CLSCompliant(false)]
        public sealed override void SetValueDirect(TypedReference obj, object value)
            => field.SetValueDirect(obj, value);

        public bool IsReadOnly => field.Attributes.HasFlag(FieldAttributes.InitOnly);

        FieldInfo IMember<FieldInfo>.RuntimeMember => field;

        public bool Equals(FieldInfo other) => field.Equals(other);

        public bool Equals(Field<T> other) => other != null && Equals(other.field);

        public sealed override int GetHashCode() => field.GetHashCode();

        public sealed override bool Equals(object other)
        {
            switch (other)
            {
                case Field<T> field:
                    return Equals(field);
                case FieldInfo field:
                    return Equals(field);
                default:
                    return false;
            }
        }

        public sealed override string ToString() => field.ToString();

        public static bool operator ==(Field<T> first, Field<T> second) => Equals(first, second);

        public static bool operator !=(Field<T> first, Field<T> second) => !Equals(first, second);
    }
    
}