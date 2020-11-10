using System.Reflection;
using System.Linq.Expressions;

namespace DotNext.Reflection
{
    using static Runtime.CompilerServices.PointerHelpers;

    public partial class Reflector
    {
        private static MemberExpression BuildFieldAccess(FieldInfo field, ParameterExpression target)
        {
            Expression? owner;
            if (field.IsStatic)
                owner = null;
            else if (field.DeclaringType.IsValueType)
                owner = Expression.Unbox(target, field.DeclaringType);
            else
                owner = Expression.Convert(target, field.DeclaringType);

            return Expression.Field(owner, field);
        }

        private static Expression BuildFieldGetter(MemberExpression field)
        {
            Expression fieldAccess = field;
            if (fieldAccess.Type.IsPointer)
                fieldAccess = Wrap(fieldAccess);
            if (fieldAccess.Type.IsValueType)
                fieldAccess = Expression.Convert(fieldAccess, typeof(object));
            return fieldAccess;
        }

        private static DynamicInvoker BuildFieldGetter(FieldInfo field)
        {
            var target = Expression.Parameter(typeof(object));
            var arguments = Expression.Parameter(typeof(object[]));
            return Expression.Lambda<DynamicInvoker>(BuildFieldGetter(BuildFieldAccess(field, target)), target, arguments).Compile();
        }

        private static Expression BuildFieldSetter(MemberExpression field, ParameterExpression arguments)
        {
            Expression valueArg = Expression.ArrayIndex(arguments, Expression.Constant(0));
            if (field.Type.IsPointer)
                valueArg = Unwrap(valueArg, field.Type);
            if (valueArg.Type != field.Type)
                valueArg = Expression.Convert(valueArg, field.Type);
            return Expression.Block(typeof(object), Expression.Assign(field, valueArg), Expression.Default(typeof(object)));
        }

        private static DynamicInvoker BuildFieldSetter(FieldInfo field)
        {
            var target = Expression.Parameter(typeof(object));
            var arguments = Expression.Parameter(typeof(object[]));
            return Expression.Lambda<DynamicInvoker>(BuildFieldSetter(BuildFieldAccess(field, target), arguments), target, arguments).Compile();
        }

        private static DynamicInvoker BuildFieldAccess(FieldInfo field)
        {
            var target = Expression.Parameter(typeof(object));
            var arguments = Expression.Parameter(typeof(object[]));
            var fieldAccess = BuildFieldAccess(field, target);
            var body = Expression.Condition(
                Expression.Equal(Expression.ArrayLength(arguments), Expression.Constant(0)),
                BuildFieldGetter(fieldAccess),
                BuildFieldSetter(fieldAccess, arguments),
                typeof(object));
            return Expression.Lambda<DynamicInvoker>(body, target, arguments).Compile();
        }
    }
}