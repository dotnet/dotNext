using System.Dynamic;
using System.Linq.Expressions;
using System.Reflection;
using Debug = System.Diagnostics.Debug;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Linq.Expressions;

using Intrinsics = Runtime.Intrinsics;

internal sealed class MetaExpression : DynamicMetaObject
{
    private static readonly MethodInfo AsExpressionBuilderMethod = new Func<object?, ISupplier<Expression>?>(Unsafe.As<ISupplier<Expression>>).Method;
    private static readonly MethodInfo AsExpressionMethod = new Func<object?, Expression?>(Unsafe.As<Expression>).Method;
    private static readonly MethodInfo BuildMethod = typeof(ISupplier<Expression>).GetMethod(nameof(ISupplier<Expression>.Invoke), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;
    private static readonly MethodInfo MakeUnaryMethod = new Func<ExpressionType, Expression, Type, UnaryExpression>(Expression.MakeUnary).Method;
    private static readonly MethodInfo MakeBinaryMethod = new Func<ExpressionType, Expression, Expression, BinaryExpression>(Expression.MakeBinary).Method;
    private static readonly MethodInfo PropertyOrFieldMethod = new Func<Expression, string, MemberExpression>(Expression.PropertyOrField).Method;
    private static readonly MethodInfo AssignMethod = new Func<Expression, Expression, BinaryExpression>(Expression.Assign).Method;
    private static readonly MethodInfo CallMethod = new Func<Expression, string, Expression[], MethodCallExpression>(ExpressionBuilder.Call).Method;
    private static readonly MethodInfo InvokeMethod = new Func<Expression, Expression[], InvocationExpression>(Expression.Invoke).Method;
    private static readonly MethodInfo NewMethod = new Func<Type, Expression[], NewExpression>(ExpressionBuilder.New).Method;
    private static readonly MethodInfo MakeIndexMethod = new Func<Expression, Expression[], IndexExpression>(ExpressionBuilder.MakeIndex).Method;
    private static readonly MethodInfo ActivateMethod = new Func<Expression, Expression[], MethodCallExpression>(ExpressionBuilder.New).Method;

    internal MetaExpression(Expression binding, ISupplier<Expression> builder)
        : base(binding, BindingRestrictions.Empty, builder)
    {
    }

    private MetaExpression(Expression binding, BindingRestrictions restrictions)
        : base(binding, restrictions)
    {
        Debug.Assert(typeof(Expression).IsAssignableFrom(LimitType));
    }

    private BindingRestrictions CreateRestrictions()
    {
        if (typeof(Expression).IsAssignableFrom(Expression.Type))
            return BindingRestrictions.Empty;

        return BindingRestrictions.GetExpressionRestriction(Expression.TypeIs(Expression, typeof(ISupplier<Expression>)));
    }

    private DynamicMetaObject NotSupportedResult(Type type)
        => new(Expression.Throw(Expression.New(typeof(NotSupportedException)), type), CreateRestrictions());

    private static Expression ToExpression(DynamicMetaObject arg, out BindingRestrictions restrictions)
    {
        restrictions = arg.Restrictions;

        // early binding cases
        if (typeof(ISupplier<Expression>).IsAssignableFrom(arg.Expression.Type))
            return Expression.Call(arg.Expression, BuildMethod);

        if (typeof(Expression).IsAssignableFrom(arg.Expression.Type))
            return arg.Expression;

        // late-binding cases
        if (arg.HasValue)
        {
            switch (arg.Value)
            {
                case System.Linq.Expressions.Expression:
                    restrictions = BindingRestrictions.GetExpressionRestriction(Expression.TypeIs(arg.Expression, typeof(Expression))).Merge(arg.Restrictions);
                    return Expression.Call(null, AsExpressionMethod, arg.Expression);
                case ISupplier<Expression>:
                    restrictions = BindingRestrictions.GetExpressionRestriction(Expression.TypeIs(arg.Expression, typeof(ISupplier<Expression>))).Merge(arg.Restrictions);
                    return Expression.Call(Expression.Call(null, AsExpressionBuilderMethod, arg.Expression), BuildMethod);
            }
        }

        return Expression.Call(typeof(ExpressionBuilder), nameof(ExpressionBuilder.Const), new[] { arg.Expression.Type }, arg.Expression);
    }

    private static IEnumerable<Expression> ToExpressions(DynamicMetaObject[] args, out BindingRestrictions restrictions)
    {
        restrictions = BindingRestrictions.Empty;

        if (Intrinsics.GetLength(args) == 0)
            return Array.Empty<Expression>();

        var result = new Expression[args.LongLength];
        for (nint i = 0; i < Intrinsics.GetLength(args); i++)
        {
            result[i] = ToExpression(args[i], out var r);
            restrictions = restrictions.Merge(r);
        }

        return result;
    }

    private Expression PrepareExpression()
        => Value is ISupplier<Expression> ?
            Expression.Call(Expression.Call(null, AsExpressionBuilderMethod, Expression), BuildMethod) :
            Expression;

    public override DynamicMetaObject BindUnaryOperation(UnaryOperationBinder binder)
    {
        var binding = PrepareExpression();
        binding = Expression.Call(MakeUnaryMethod, binder.Operation.Const(), binding, default(Type).Const());
        return new MetaExpression(binding, CreateRestrictions());
    }

    public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
    {
        var binding = PrepareExpression();
        binding = Expression.Call(PropertyOrFieldMethod, binding, binder.Name.Const());
        binding = Expression.Call(AssignMethod, binding, ToExpression(value, out var restrictions));
        return new MetaExpression(binding, CreateRestrictions().Merge(restrictions));
    }

    public override DynamicMetaObject BindInvoke(InvokeBinder binder, DynamicMetaObject[] args)
    {
        var binding = PrepareExpression();
        binding = Expression.Call(InvokeMethod, binding, Expression.NewArrayInit(typeof(Expression), ToExpressions(args, out var restrictions)));
        return new MetaExpression(binding, CreateRestrictions().Merge(restrictions));
    }

    public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
    {
        var binding = PrepareExpression();
        binding = Expression.Call(CallMethod, binding, binder.Name.Const(), Expression.NewArrayInit(typeof(Expression), ToExpressions(args, out var restrictions)));
        return new MetaExpression(binding, CreateRestrictions().Merge(restrictions));
    }

    public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
    {
        var binding = PrepareExpression();
        binding = Expression.Call(MakeIndexMethod, binding, Expression.NewArrayInit(typeof(Expression), ToExpressions(indexes, out var restrictions)));
        return new MetaExpression(binding, CreateRestrictions().Merge(restrictions));
    }

    public override DynamicMetaObject BindSetIndex(SetIndexBinder binder, DynamicMetaObject[] indexes, DynamicMetaObject value)
    {
        var binding = PrepareExpression();
        binding = Expression.Call(MakeIndexMethod, binding, Expression.NewArrayInit(typeof(Expression), ToExpressions(indexes, out var indexesRestrictions)));
        binding = Expression.Call(AssignMethod, binding, ToExpression(value, out var valueRestrictions));
        return new MetaExpression(binding, CreateRestrictions().Merge(indexesRestrictions).Merge(valueRestrictions));
    }

    public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
    {
        var binding = PrepareExpression();
        binding = Expression.Call(PropertyOrFieldMethod, binding, binder.Name.Const());
        return new MetaExpression(binding, CreateRestrictions());
    }

    public override DynamicMetaObject BindBinaryOperation(BinaryOperationBinder binder, DynamicMetaObject arg)
    {
        var left = PrepareExpression();
        left = Expression.Call(MakeBinaryMethod, binder.Operation.Const(), left, ToExpression(arg, out var restrictions));
        return new MetaExpression(left, CreateRestrictions().Merge(restrictions));
    }

    public override DynamicMetaObject BindConvert(ConvertBinder binder)
    {
        var binding = PrepareExpression();
        if (binder.Type == typeof(Expression))
            return new DynamicMetaObject(binding, CreateRestrictions());

        return NotSupportedResult(binder.Type);
    }

    public override DynamicMetaObject BindDeleteMember(DeleteMemberBinder binder)
        => NotSupportedResult(binder.ReturnType);

    public override DynamicMetaObject BindDeleteIndex(DeleteIndexBinder binder, DynamicMetaObject[] indexes)
        => NotSupportedResult(binder.ReturnType);

    public override DynamicMetaObject BindCreateInstance(CreateInstanceBinder binder, DynamicMetaObject[] args)
    {
        var binding = PrepareExpression();
        BindingRestrictions restrictions;
        binding = Expression is ConstantExpression and { Value: ConstantExpression constExpr and { Value: Type } } ?
            Expression.Call(NewMethod, constExpr, Expression.NewArrayInit(typeof(Expression), ToExpressions(args, out restrictions))) :
            Expression.Call(ActivateMethod, binding, Expression.NewArrayInit(typeof(Expression), ToExpressions(args, out restrictions)));

        return new MetaExpression(binding, CreateRestrictions().Merge(restrictions));
    }
}