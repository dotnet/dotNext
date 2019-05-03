using System;
using System.Reflection;

namespace DotNext.Reflection
{
    /// <summary>
    /// Contains static methods for retrieving custom attributes.
    /// </summary>
    public static class CustomAttribute
    {
        /// <summary>
        /// Indicates whether one or more attributes of the specified type or of its derived types is applied to the member.
        /// </summary>
        /// <typeparam name="A">The type of custom attribute to search for. The search includes derived types.</typeparam>
        /// <param name="member">The member to inspect.</param>
        /// <param name="inherit"><see langword="true"/> to search this member's inheritance chain to find the attributes; otherwise, <see langword="false"/>. This parameter is ignored for properties and events.</param>
        /// <returns><see langword="true"/> if one or more instances of <typeparamref name="A"/> or any of its derived types is applied to the provided member; otherwise, <see langword="false"/>.</returns>
        public static bool IsDefined<A>(this MemberInfo member, bool inherit = false) where A : Attribute => member.IsDefined(typeof(A), inherit);

        /// <summary>
        /// Returns a value that indicates whether the specified attribute type has been applied to the module.
        /// </summary>
        /// <param name="module">The module to inspect.</param>
        /// <typeparam name="A">The type of custom attribute to search for. The search includes derived types.</typeparam>
        /// <returns><see langword="true"/> if one or more instances of <typeparamref name="A"/> or any of its derived types is applied to the provided module; otherwise, <see langword="false"/>.</returns>
        public static bool IsDefined<A>(this Module module) where A : Attribute => module.IsDefined(typeof(A), false);

        /// <summary>
        /// Returns a value that indicates whether the specified attribute type has been applied to the assembly.
        /// </summary>
        /// <param name="assembly">The assembly to inspect.</param>
        /// <typeparam name="A">The type of custom attribute to search for. The search includes derived types.</typeparam>
        /// <returns><see langword="true"/> if one or more instances of <typeparamref name="A"/> or any of its derived types is applied to the provided assembly; otherwise, <see langword="false"/>.</returns>
        public static bool IsDefined<A>(this Assembly assembly) where A : Attribute => assembly.IsDefined(typeof(A), false);

        /// <summary>
        /// Returns a value that indicates whether the specified attribute type has been applied to the parameter.
        /// </summary>
        /// <param name="parameter">The parameter to inspect.</param>
        /// <typeparam name="A">The type of custom attribute to search for. The search includes derived types.</typeparam>
        /// <returns><see langword="true"/> if one or more instances of <typeparamref name="A"/> or any of its derived types is applied to the provided parameter; otherwise, <see langword="false"/>.</returns>
        public static bool IsDefined<A>(this ParameterInfo parameter) where A : Attribute => parameter.IsDefined(typeof(A), false);
    }
}