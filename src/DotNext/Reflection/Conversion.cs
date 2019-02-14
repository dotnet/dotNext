using System;
using static System.Linq.Expressions.Expression;

namespace DotNext.Reflection
{
    /// <summary>
    /// Provides access to implicit or explicit type conversion
    /// operator between two types.
    /// </summary>
    /// <typeparam name="I">Source type to convert.</typeparam>
    /// <typeparam name="O">Type of conversion result.</typeparam>
    public static class Conversion<I, O>
    {
        /// <summary>
        /// Represents implicit or explicit cast operator
        /// wrapped into delegate.
        /// </summary>
        public static readonly Converter<I, O> Converter;
        /// <summary>
        /// Indicates that overloaded type-cast operator between types <typeparamref name="I"/> and <typeparamref name="O"/>
        /// is present.
        /// </summary>
        public static readonly bool Supported;

        static Conversion()
        {
            try
            {
                Converter = Convert(Default(typeof(I)), typeof(O)).Method.CreateDelegate<Converter<I, O>>();
                Supported = true;
            }
            catch (InvalidOperationException e)
            {
                Converter = input => throw e;
                Supported = false;
            }
        }
    }
}
