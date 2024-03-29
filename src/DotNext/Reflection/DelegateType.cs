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
    /// Returns special Invoke method generate for each delegate type.
    /// </summary>
    /// <typeparam name="TDelegate">Type of delegate.</typeparam>
    /// <returns>An object representing reflected method Invoke.</returns>
    /// <exception cref="GenericArgumentException{G}"><typeparamref name="TDelegate"/> is not a concrete delegate type.</exception>
    public static MethodInfo GetInvokeMethod<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TDelegate>()
        where TDelegate : Delegate
    {
        return typeof(TDelegate) is { IsSealed: true } delegateType ?
            delegateType.GetMethod(InvokeMethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)!
            : throw new GenericArgumentException<TDelegate>(ExceptionMessages.ConcreteDelegateExpected);
    }
}