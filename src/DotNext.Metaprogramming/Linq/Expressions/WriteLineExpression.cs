using System;
using System.IO;
using System.Linq.Expressions;

namespace DotNext.Linq.Expressions
{
    /// <summary>
    /// Represents expression that writes some object into particular output.
    /// </summary>
    /// <seealso cref="Console.Out"/>
    /// <seealso cref="Console.Error"/>
    /// <seealso cref="TextWriter.WriteLine(object)"/>
    /// <seealso cref="System.Diagnostics.Debug.WriteLine(object)"/>
    public sealed class WriteLineExpression : CustomExpression
    {
        private enum Kind : byte
        {
            Out = 0,

            Error,

            Debug,
        }

        private readonly Kind kind;
        private readonly Expression value;

        private WriteLineExpression(Expression value, Kind kind)
        {
            this.value = value;
            this.kind = kind;
        }

        /// <summary>
        /// Always returns <see cref="void"/>.
        /// </summary>
        public override Type Type => typeof(void);

        /// <summary>
        /// Creates an expression that writes the object into <see cref="Console.Out"/>.
        /// </summary>
        /// <param name="value">The value to be written into the stdout.</param>
        /// <returns>A new instance of <see cref="WriteLineExpression"/>.</returns>
        public static WriteLineExpression Out(Expression value) => new WriteLineExpression(value, Kind.Out);

        /// <summary>
        /// Creates an expression that writes the object into <see cref="Console.Error"/>.
        /// </summary>
        /// <param name="value">The value to be written into the stderr.</param>
        /// <returns>A new instance of <see cref="WriteLineExpression"/>.</returns>
        public static WriteLineExpression Error(Expression value) => new WriteLineExpression(value, Kind.Error);

        /// <summary>
        /// Creates an expression that writes the object using <see cref="System.Diagnostics.Debug.WriteLine(object)"/>.
        /// </summary>
        /// <param name="value">The value to be written into the stderr.</param>
        /// <returns>A new instance of <see cref="WriteLineExpression"/>.</returns>
        public static WriteLineExpression Debug(Expression value) => new WriteLineExpression(value, Kind.Debug);

        private static MethodCallExpression WriteLineTo(MemberExpression stream, Expression value)
        {
            var writeLineMethod = typeof(TextWriter).GetMethod(nameof(TextWriter.WriteLine), new[] { value.Type });
            if (writeLineMethod != null)
                return Call(stream, writeLineMethod, value);
            if (value.Type.IsValueType)
                value = Convert(value, typeof(object));
            writeLineMethod = typeof(TextWriter).GetMethod(nameof(TextWriter.WriteLine), new[] { typeof(object) });
            return Call(stream, writeLineMethod, value);
        }

        private MethodCallExpression WriteLineToOut()
        {
            var outProperty = typeof(Console).GetProperty(nameof(Console.Out));
            return WriteLineTo(Property(null, outProperty), value);
        }

        private MethodCallExpression WriteLineToError()
        {
            var outProperty = typeof(Console).GetProperty(nameof(Console.Error));
            return WriteLineTo(Property(null, outProperty), value);
        }

        private MethodCallExpression WriteLineToDebug()
        {
            var writeLineMethod = typeof(System.Diagnostics.Debug).GetMethod(nameof(System.Diagnostics.Debug.WriteLine), new[] { typeof(object) });
            return Call(writeLineMethod, value.Type.IsValueType ? Convert(value, typeof(object)) : value);
        }

        /// <summary>
        /// Translates this expression into predefined set of expressions
        /// using Lowering technique.
        /// </summary>
        /// <returns>Translated expression.</returns>
        public override Expression Reduce()
        {
            switch (kind)
            {
                case Kind.Out:
                    return WriteLineToOut();
                case Kind.Error:
                    return WriteLineToError();
                case Kind.Debug:
                    return WriteLineToDebug();
                default:
                    return Empty();
            }
        }
    }
}