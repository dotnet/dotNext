using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using FormattableStringFactory = System.Runtime.CompilerServices.FormattableStringFactory;

namespace DotNext.Linq.Expressions
{
    /// <summary>
    /// Represents string interpolation expression.
    /// </summary>
    /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/tokens/interpolated">String Interpolation in C#</seealso>
    /// <seealso href="https://docs.microsoft.com/en-us/dotnet/visual-basic/programming-guide/language-features/strings/interpolated-strings">String Interpolation in VB.NET</seealso>
    public sealed class InterpolationExpression : CustomExpression
    {
        private enum Kind : byte
        {
            PlainString = 0,
            FormattableString,
        }

        private readonly Expression[] arguments;
        private readonly Kind kind;

        private InterpolationExpression(string format, object?[] arguments, Kind kind)
        {
            this.arguments = Array.ConvertAll(arguments, GetArgument);
            Format = format;
            this.kind = kind;

            static Expression GetArgument(object? arg) => arg switch
            {
                null => Constant(null, typeof(object)),
                Expression expr => expr,
                _ => Constant(arg),
            };
        }

        private InterpolationExpression(FormattableString str, Kind kind)
            : this(str.Format, str.GetArguments(), kind)
        {
        }

        /// <summary>
        /// Returns string interpolation expression which produces
        /// instance of <see cref="System.FormattableString"/> class.
        /// </summary>
        /// <param name="str">Formatting pattern and actual arguments.</param>
        /// <returns>String interpolation expression.</returns>
        public static InterpolationExpression FormattableString(FormattableString str)
            => new InterpolationExpression(str, Kind.FormattableString);

        /// <summary>
        /// Returns string interpolation expression which produces
        /// formatted string as <see cref="string"/> class.
        /// </summary>
        /// <param name="str">Formatting pattern and actual arguments.</param>
        /// <returns>String interpolation expression.</returns>
        public static InterpolationExpression PlainString(FormattableString str)
            => new InterpolationExpression(str, Kind.PlainString);

        /// <summary>
        /// Returns a collection that contains one or more objects to format.
        /// </summary>
        public IReadOnlyList<Expression> Arguments => arguments;

        /// <summary>
        /// Gets formatting pattern.
        /// </summary>
        public string Format { get; }

        /// <summary>
        /// Gets type of this expression.
        /// </summary>
        /// <remarks>
        /// May be <see cref="string"/> or <see cref="System.FormattableString"/>
        /// which is depends on factory method.
        /// </remarks>
        public override Type Type
        {
            get => kind switch
            {
                Kind.PlainString => typeof(string),
                Kind.FormattableString => typeof(FormattableString),
                _ => typeof(void),
            };
        }

        private Expression MakePlainString()
        {
            // string.Format(format, arguments)
            switch (arguments.LongLength)
            {
                case 0:
                    return Constant(Format);
                case 1:
                    var formatMethod = typeof(string).GetMethod(nameof(string.Format), new[] { typeof(string), typeof(object) });
                    Debug.Assert(formatMethod is not null);
                    return Call(formatMethod, Constant(Format), arguments[0]);
                case 2:
                    formatMethod = typeof(string).GetMethod(nameof(string.Format), new[] { typeof(string), typeof(object), typeof(object) });
                    Debug.Assert(formatMethod is not null);
                    return Call(formatMethod, Constant(Format), arguments[0], arguments[1]);
                case 3:
                    formatMethod = typeof(string).GetMethod(nameof(string.Format), new[] { typeof(string), typeof(object), typeof(object), typeof(object) });
                    Debug.Assert(formatMethod is not null);
                    return Call(formatMethod, Constant(Format), arguments[0], arguments[1], arguments[2]);
                default:
                    formatMethod = typeof(string).GetMethod(nameof(string.Format), new[] { typeof(string), typeof(object[]) });
                    Debug.Assert(formatMethod is not null);
                    return Call(formatMethod, Constant(Format), NewArrayInit(typeof(object), arguments));
            }
        }

        private MethodCallExpression MakeFormattableString()
            => typeof(FormattableStringFactory).CallStatic(nameof(FormattableStringFactory.Create), Constant(Format), NewArrayInit(typeof(object), arguments));

        /// <summary>
        /// Translates this expression into predefined set of expressions
        /// using Lowering technique.
        /// </summary>
        /// <returns>Translated expression.</returns>
        public override Expression Reduce() => kind switch
        {
            Kind.PlainString => MakePlainString(),
            Kind.FormattableString => MakeFormattableString(),
            _ => Empty(),
        };
    }
}
