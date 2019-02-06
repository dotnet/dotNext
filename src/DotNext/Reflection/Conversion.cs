using System;
using MethodInfo = System.Reflection.MethodInfo;
using static System.Linq.Expressions.Expression;

namespace DotNext.Reflection
{
    /// <summary>
    /// Provides access to implicit or explicit type conversion
    /// operator between two types.
    /// </summary>
    /// <typeparam name="I"></typeparam>
    /// <typeparam name="O"></typeparam>
    public static class Conversion<I, O>
    {
        /// <summary>
        /// Represents implicit or explicit cast operator
        /// wrapped into delegate.
        /// </summary>
        public static readonly Converter<I, O> Converter;

        static Conversion()
        {
            MethodInfo converter;
            try
            {
                converter = Convert(Default(typeof(I)), typeof(O)).Method;
            }
            catch (InvalidOperationException)
            {
                converter = null;
            }
            Converter = converter?.CreateDelegate<Converter<I, O>>();
        }
    }
}
