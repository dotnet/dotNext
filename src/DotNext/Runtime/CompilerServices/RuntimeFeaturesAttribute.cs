using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Runtime.CompilerServices
{
    /// <summary>
    /// Indicates that the code inside of method, type, module or assembly relies
    /// on specific runtime features.
    /// </summary>
    /// <remarks>
    /// This attribute informs the developer about potential portability and performance
    /// issues associated with the marked program element.
    /// </remarks>
    [SuppressMessage("Style", "CA1051", Justification = "This type for internal purposes only")]
    [Conditional("DEBUG")]
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Assembly | AttributeTargets.Module, AllowMultiple = false, Inherited = true)]
    public sealed class RuntimeFeaturesAttribute : Attribute
    {
        /// <summary>
        /// Indicates that code relies on dynamic IL code generation or compilation of Expression Trees.
        /// </summary>
        public bool DynamicCodeCompilation;

        /// <summary>
        /// Indicates that code relies on <see cref="System.Reflection.MethodInfo.MakeGenericMethod(Type[])"/>
        /// or <see cref="Type.MakeGenericType(Type[])"/> calls.
        /// </summary>
        public bool RuntimeGenericInstantiation;

        /// <summary>
        /// Indicates that code relies on reflection of private or internal members.
        /// </summary>
        public bool PrivateReflection;

        /// <summary>
        /// Indicates that code is augmented using .NEXT Weaver.
        /// </summary>
        public bool Augmentation;
    }
}