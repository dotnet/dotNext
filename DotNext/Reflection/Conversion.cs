using System;
using System.Reflection;

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
        private static readonly bool @implicit;
        public static readonly Converter<I, O> Converter;

        static Conversion()
        {
            const string ImplicitOperator = "op_Implicit";
            const string ExplicitOperator = "op_Explicit";

            Type inputType = typeof(I), outputType = typeof(O);
            if (TryCreate(inputType, ImplicitOperator, out var converter) ||
                TryCreate(outputType, ImplicitOperator, out converter))
            {
                Converter = converter;
                @implicit = true;
            }
            else if (TryCreate(inputType, ExplicitOperator, out converter) ||
                TryCreate(outputType, ExplicitOperator, out converter))
            {
                Converter = converter;
                @implicit = false;
            }
            else
                Converter = null;
        }

        private static bool TryCreate(Type t, string operatorName, out Converter<I, O> output)
        {
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;
            return TryCreate(t.GetMethod(operatorName, Flags, Type.DefaultBinder, new[] { typeof(I) }, Array.Empty<ParameterModifier>()), out output);
        }

        private static bool TryCreate(MethodInfo converter, out Converter<I, O> output)
        {
            if(!(converter is null) && typeof(O).IsAssignableFromWithoutBoxing(converter.ReturnType))
            {
                output = converter.CreateDelegate<Converter<I, O>>();
                return true;
            }
            else
            {
                output = null;
                return false;
            }
        }

        public static bool IsImplicit => Converter is null ?
            throw new InvalidOperationException(string.Format(ExceptionMessages.NoConversionBetweenTypes, typeof(I), typeof(O))) :
            @implicit;
    }
}
