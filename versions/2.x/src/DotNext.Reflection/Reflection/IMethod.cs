using System;
using System.Reflection;

namespace DotNext.Reflection
{
    /// <summary>
    /// Represents reflected statically typed method.
    /// </summary>
    /// <typeparam name="TMethod">The type of the method.</typeparam>
    /// <typeparam name="TSignature">Type of delegate describing method signature.</typeparam>
    public interface IMethod<out TMethod, out TSignature> : IMember<TMethod, TSignature>
        where TMethod : MethodBase
        where TSignature : Delegate
    {
    }

    /// <summary>
    /// Represents regular method.
    /// </summary>
    /// <typeparam name="TSignature">Type of delegate describing method signature.</typeparam>
    public interface IMethod<out TSignature> : IMethod<MethodInfo, TSignature>
        where TSignature : Delegate
    {
    }
}
