using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Reflection;

/// <summary>
/// Represents operator.
/// </summary>
/// <typeparam name="TSignature">Type of delegate describing signature of operator.</typeparam>
public interface IOperator<out TSignature> : IMember<MemberInfo, TSignature>
    where TSignature : Delegate
{
    /// <summary>
    /// Gets type of operator.
    /// </summary>
    ExpressionType Type { get; }

    /// <inheritdoc/>
    object[] ICustomAttributeProvider.GetCustomAttributes(bool inherit) => Metadata.GetCustomAttributes(inherit);

    /// <inheritdoc/>
    object[] ICustomAttributeProvider.GetCustomAttributes(Type attributeType, bool inherit) => Metadata.GetCustomAttributes(attributeType, inherit);

    /// <inheritdoc/>
    bool ICustomAttributeProvider.IsDefined(Type attributeType, bool inherit) => Metadata.IsDefined(attributeType, inherit);
}