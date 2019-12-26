using System;
using System.ComponentModel;

namespace DotNext.Runtime.CompilerServices
{
    /// <summary>
    /// Marks property as a provide of distributed service.
    /// </summary>
    /// <remarks>
    /// This attribute is not indendent to be used directly in your code.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class DistributedServiceProviderAttribute : Attribute
    {
    }
}