using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotNext.Reflection;

/// <summary>
/// Reflects inline arrays.
/// </summary>
/// <seealso cref="InlineArrayAttribute"/>
public static class InlineArrayType
{
    /// <summary>
    /// Provides Reflection over inline arrays.
    /// </summary>
    /// <param name="type">The type that is potentially an inline array.</param>
    extension([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.AllFields)] Type type)
    {
        /// <summary>
        /// Gets a value indicating that the type is inline array.
        /// </summary>
        public bool IsInlineArray => type.IsValueType && type.IsDefined<InlineArrayAttribute>();

        /// <summary>
        /// Gets inline array element type and size.
        /// </summary>
        /// <value>The size and element type of the inline array. Size is zero, if target type is not an inline array.</value>
        public (int Size, Type ElementType) InlineArrayTypeAndSize
        {
            get
            {
                (int Size, Type ElementType) result;
                if (type.GetCustomAttribute<InlineArrayAttribute>() is { } attribute)
                {
                    result.Size = attribute.Length;
                    result.ElementType = type
                        .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly | BindingFlags.Instance)
                        .FirstOrNone()
                        .ValueOrDefault?.FieldType ?? typeof(void);
                }
                else
                {
                    result.Size = 0;
                    result.ElementType = typeof(void);
                }

                return result;
            }
        }
    }
}