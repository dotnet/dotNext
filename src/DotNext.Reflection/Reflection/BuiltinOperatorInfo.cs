using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace DotNext.Reflection
{
    using Enumerable = System.Linq.Enumerable;
    using ExpressionType = System.Linq.Expressions.ExpressionType;

    internal sealed class BuiltinOperatorInfo : MemberInfo, IEquatable<BuiltinOperatorInfo>
    {
        private readonly ExpressionType expression;

        internal BuiltinOperatorInfo(Type declaringType, ExpressionType op)
        {
            DeclaringType = declaringType;
            expression = op;
        }

        public override Type DeclaringType { get; }

        public override MemberTypes MemberType => MemberTypes.Custom;

        public override string Name => expression.ToString();

        public override Type? ReflectedType => DeclaringType.ReflectedType;

        public override object[] GetCustomAttributes(bool inherit) => Array.Empty<object>();

        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => Array.Empty<object>();

        public override bool IsDefined(Type attributeType, bool inherit) => false;

        public override bool HasSameMetadataDefinitionAs(MemberInfo other) => other is BuiltinOperatorInfo operatorInfo && expression == operatorInfo.expression;

        public override IEnumerable<CustomAttributeData> CustomAttributes => Enumerable.Empty<CustomAttributeData>();

        public override IList<CustomAttributeData> GetCustomAttributesData() => Array.Empty<CustomAttributeData>();

        public override Module Module => typeof(string).Module; // built-in operators always in core module

        public override int MetadataToken => 0;

        public override string ToString() => expression.ToString();

        public bool Equals(BuiltinOperatorInfo? other)
            => other is not null && expression == other.expression && DeclaringType == other.DeclaringType;

        public override bool Equals([NotNullWhen(true)] object? other) => Equals(other as BuiltinOperatorInfo);

        public override int GetHashCode() => HashCode.Combine(expression, DeclaringType);
    }
}
