using System;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Reflection
{
    /// <summary>
    /// Represents operator.
    /// </summary>
    /// <typeparam name="D">Type of delegate describing signature of operator.</typeparam>
    public interface IOperator<out D> : IMember<MemberInfo, D>
        where D : Delegate
    {
        /// <summary>
        /// Gets type of operator.
        /// </summary>
        ExpressionType Type { get; }

        object[] ICustomAttributeProvider.GetCustomAttributes(bool inherit) => RuntimeMember.GetCustomAttributes(inherit);

        object[] ICustomAttributeProvider.GetCustomAttributes(Type attributeType, bool inherit) => RuntimeMember.GetCustomAttributes(attributeType, inherit);

        bool ICustomAttributeProvider.IsDefined(Type attributeType, bool inherit) => RuntimeMember.IsDefined(attributeType, inherit);
    }
}