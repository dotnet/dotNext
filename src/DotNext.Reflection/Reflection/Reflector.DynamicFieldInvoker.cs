using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Reflection;

using static Runtime.CompilerServices.ReflectionUtils;

public static partial class Reflector
{
    private static MemberExpression BuildFieldAccess(FieldInfo field, ParameterExpression target)
    {
        Expression? owner = field switch
        {
            { IsStatic: true } or { DeclaringType: null } => null,
            { DeclaringType: { IsValueType: true } } => Expression.Unbox(target, field.DeclaringType),
            _ => Expression.Convert(target, field.DeclaringType)
        };

        return Expression.Field(owner, field);
    }

    private static Expression BuildFieldGetter(MemberExpression field, bool volatileAccess)
    {
        Expression fieldAccess = field;

        if (volatileAccess)
            fieldAccess = VolatileRead(fieldAccess);

        if (fieldAccess.Type.IsPointer)
            fieldAccess = Wrap(fieldAccess);

        if (fieldAccess.Type.IsValueType)
            fieldAccess = Expression.Convert(fieldAccess, typeof(object));

        return fieldAccess;
    }

    private static DynamicInvoker BuildFieldGetter(FieldInfo field, bool volatileAccess)
    {
        var target = Expression.Parameter(typeof(object));
        var arguments = Expression.Parameter(typeof(Span<object?>));
        return Expression.Lambda<DynamicInvoker>(BuildFieldGetter(BuildFieldAccess(field, target), volatileAccess), target, arguments).Compile();
    }

    private static Expression BuildFieldSetter(MemberExpression field, ParameterExpression arguments, bool volatileAccess)
    {
        Expression getter = Get(arguments, Expression.Constant(0));
        if (field.Type.IsPointer)
            getter = Unwrap(getter, field.Type);
        if (getter.Type != field.Type)
            getter = Expression.Convert(getter, field.Type);

        Expression body = volatileAccess ?
            VolatileWrite(field, getter) :
            Expression.Assign(field, getter);
        return Expression.Block(typeof(object), body, Expression.Default(typeof(object)));
    }

    private static DynamicInvoker BuildFieldSetter(FieldInfo field, bool volatileAccess)
    {
        var target = Expression.Parameter(typeof(object));
        var arguments = Expression.Parameter(typeof(Span<object?>));
        return Expression.Lambda<DynamicInvoker>(BuildFieldSetter(BuildFieldAccess(field, target), arguments, volatileAccess), target, arguments).Compile();
    }

    private static DynamicInvoker BuildFieldAccess(FieldInfo field, bool volatileAccess)
    {
        var target = Expression.Parameter(typeof(object));
        var arguments = Expression.Parameter(typeof(Span<object?>));
        var fieldAccess = BuildFieldAccess(field, target);
        var body = Expression.Condition(
            Expression.Equal(SpanLength(arguments), Expression.Constant(0)),
            BuildFieldGetter(fieldAccess, volatileAccess),
            BuildFieldSetter(fieldAccess, arguments, volatileAccess),
            typeof(object));
        return Expression.Lambda<DynamicInvoker>(body, target, arguments).Compile();
    }
}