using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace DotNext.Reflection;

/// <summary>
/// Provides specialized reflection methods for
/// delegate types.
/// </summary>
public static class DelegateType
{
    private const string InvokeMethodName = nameof(Action.Invoke);

    /// <summary>
    /// Extends delegate types.
    /// </summary>
    /// <typeparam name="TDelegate">Type of delegate.</typeparam>
    extension<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TDelegate>(TDelegate)
        where TDelegate : Delegate
    {
        /// <summary>
        /// Returns special Invoke method generate for each delegate type.
        /// </summary>
        /// <value>An object representing reflected method Invoke.</value>
        /// <exception cref="GenericArgumentException{G}"><typeparamref name="TDelegate"/> is not a concrete delegate type.</exception>
        public static MethodInfo InvokeMethod
            => typeof(TDelegate) is { IsSealed: true } delegateType ?
                delegateType.GetMethod(InvokeMethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)!
                : throw new GenericArgumentException<TDelegate>(ExceptionMessages.ConcreteDelegateExpected);
    }

    /// <summary>
    /// Extends <see cref="Type"/> type.
    /// </summary>
    /// <param name="delegateType">The type to extend.</param>
    extension(Type delegateType)
    {
        /// <summary>
        /// Gets a value indicating that the type represents a delegate.
        /// </summary>
        public bool IsDelegate => typeof(Delegate).IsAssignableFrom(delegateType) && delegateType.IsSealed;
    }
}