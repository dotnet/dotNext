using System.Reflection;

namespace DotNext.Reflection;

/// <summary>
/// Represents typed access to type constructor.
/// </summary>
/// <typeparam name="TSignature">Type of delegate representing constructor signature.</typeparam>
public interface IConstructor<out TSignature> : IMethod<ConstructorInfo, TSignature>
    where TSignature : Delegate
{
}