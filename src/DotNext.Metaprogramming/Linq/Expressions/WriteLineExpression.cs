using System;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Linq.Expressions
{
    public sealed class WriteLineExpression : Expression
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

        public override Type Type => typeof(void);

        public override bool CanReduce => true;

        public override ExpressionType NodeType => ExpressionType.Extension;

        public static WriteLineExpression Out(Expression value) => new WriteLineExpression(value, Kind.Out);

        public static WriteLineExpression Error(Expression value) => new WriteLineExpression(value, Kind.Error);

        public static WriteLineExpression Debug(Expression value) => new WriteLineExpression(value, Kind.Debug);

        private static MethodCallExpression WriteLineTo(MemberExpression stream, Expression value)
        {
            var writeLineMethod = typeof(TextWriter).GetMethod(nameof(TextWriter.WriteLine), new [] { value.Type });
            if(value.Type.IsValueType)
                value = Convert(value, typeof(object));
            if(writeLineMethod is null)
                writeLineMethod = typeof(TextWriter).GetMethod(nameof(TextWriter.WriteLine), new [] { typeof(object) });
            return Call(stream, writeLineMethod, value);
        }

        private MethodCallExpression WriteLineToOut()
        {
            var outProperty = typeof(Console).GetMethod(nameof(Console.Out));
            return WriteLineTo(Property(null, outProperty), value);
        }

        private MethodCallExpression WriteLineToError()
        {
            var outProperty = typeof(Console).GetMethod(nameof(Console.Error));
            return WriteLineTo(Property(null, outProperty), value);
        }

        private MethodCallExpression WriteLineToDebug()
        {
            var writeLineMethod = typeof(System.Diagnostics.Debug).GetMethod(nameof(System.Diagnostics.Debug.WriteLine), new [] { typeof(object) });
            return Call(writeLineMethod, value.Type.IsValueType ? Convert(value, typeof(object)) : value);
        }

        public override Expression Reduce()
        {
            switch(kind)
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