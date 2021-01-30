using System;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Linq.Expressions
{
    internal sealed class MetaExpression : DynamicMetaObject
    {
        private static readonly MethodInfo AsExpressionBuilderMethod = new Func<object?, IExpressionBuilder<Expression>?>(Unsafe.As<IExpressionBuilder<Expression>>).Method;
        private static readonly MethodInfo AsExpressionMethod = new Func<object?, Expression?>(Unsafe.As<Expression>).Method;
        private static readonly MethodInfo BuildMethod = typeof(IExpressionBuilder<Expression>).GetMethod(nameof(IExpressionBuilder<Expression>.Build), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;
        private static readonly MethodInfo MakeUnaryMethod = new Func<ExpressionType, Expression, Type, UnaryExpression>(Expression.MakeUnary).Method;
        private static readonly MethodInfo MakeBinaryMethod = new Func<ExpressionType, Expression, Expression, BinaryExpression>(Expression.MakeBinary).Method;
        private static readonly MethodInfo PropertyOrFieldMethod = new Func<Expression, string, MemberExpression>(Expression.PropertyOrField).Method;
        private static readonly MethodInfo AssignMethod = new Func<Expression, Expression, BinaryExpression>(Expression.Assign).Method;
        private static readonly MethodInfo CallMethod = new Func<Expression, string, Expression[], MethodCallExpression>(ExpressionBuilder.Call).Method;
        private static readonly MethodInfo InvokeMethod = new Func<Expression, Expression[], InvocationExpression>(Expression.Invoke).Method;
        private static readonly MethodInfo NewMethod = new Func<Type, Expression[], NewExpression>(ExpressionBuilder.New).Method;
        private static readonly MethodInfo PropertyMethod = new Func<Expression, string, Expression[], IndexExpression>(Expression.Property).Method;
        private static readonly MethodInfo ActivateMethod = new Func<Expression, Expression[], MethodCallExpression>(ExpressionBuilder.New).Method;

        private static readonly ConstantExpression ItemName = "Item".Const();
        private static readonly ConstantExpression ConvertOperator = ExpressionType.Convert.Const();

        internal MetaExpression(Expression binding, IExpressionBuilder<Expression> builder)
            : base(binding, BindingRestrictions.GetExpressionRestriction(Expression.TypeIs(binding, typeof(IExpressionBuilder<Expression>))), builder)
        {
        }

        private MetaExpression(Expression binding, BindingRestrictions restrictions)
            : base(binding, restrictions)
        {
        }

        private static Expression ToExpression(DynamicMetaObject arg)
        {
            if (arg is MetaExpression meta)
                return meta.PrepareExpression();

            if (typeof(Expression).IsAssignableFrom(arg.LimitType))
                return arg.Expression.Type == typeof(Expression) ? arg.Expression : Expression.Call(null, AsExpressionMethod, arg.Expression);

            return Expression.Call(typeof(ExpressionBuilder), nameof(ExpressionBuilder.Const), new[] { arg.LimitType }, arg.Expression);
        }

        private Expression PrepareExpression()
            => Value is IExpressionBuilder<Expression> ?
                Expression.Call(Expression.Call(null, AsExpressionBuilderMethod, Expression), BuildMethod) :
                Expression;

        public override DynamicMetaObject BindUnaryOperation(UnaryOperationBinder binder)
        {
            var binding = PrepareExpression();
            binding = Expression.Call(MakeUnaryMethod, binder.Operation.Const(), binding, default(Type).Const());
            return new MetaExpression(binding, Restrictions);
        }

        public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
        {
            var binding = PrepareExpression();
            binding = Expression.Call(PropertyOrFieldMethod, binding, binder.Name.Const());
            binding = Expression.Call(AssignMethod, binding, ToExpression(value));
            return new MetaExpression(binding, Restrictions);
        }

        public override DynamicMetaObject BindInvoke(InvokeBinder binder, DynamicMetaObject[] args)
        {
            var binding = PrepareExpression();
            binding = Expression.Call(InvokeMethod, binding, Expression.NewArrayInit(typeof(Expression), args.Select(ToExpression)));
            return new MetaExpression(binding, Restrictions);
        }

        public override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
        {
            var binding = PrepareExpression();
            binding = Expression.Call(CallMethod, binding, binder.Name.Const(), Expression.NewArrayInit(typeof(Expression), args.Select(ToExpression)));
            return new MetaExpression(binding, Restrictions);
        }

        public override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
        {
            var binding = PrepareExpression();
            binding = Expression.Call(PropertyMethod, binding, ItemName, Expression.NewArrayInit(typeof(Expression), indexes.Select(ToExpression)));
            return new MetaExpression(binding, Restrictions);
        }

        public override DynamicMetaObject BindSetIndex(SetIndexBinder binder, DynamicMetaObject[] indexes, DynamicMetaObject value)
        {
            var binding = PrepareExpression();
            binding = Expression.Call(PropertyMethod, binding, ItemName, Expression.NewArrayInit(typeof(Expression), indexes.Select(ToExpression)));
            binding = Expression.Call(AssignMethod, binding, ToExpression(value));
            return new MetaExpression(binding, Restrictions);
        }

        public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
        {
            var binding = PrepareExpression();
            binding = Expression.Call(PropertyOrFieldMethod, binding, binder.Name.Const());
            return new MetaExpression(binding, Restrictions);
        }

        public override DynamicMetaObject BindBinaryOperation(BinaryOperationBinder binder, DynamicMetaObject arg)
        {
            var left = PrepareExpression();
            left = Expression.Call(MakeBinaryMethod, binder.Operation.Const(), left, ToExpression(arg));
            return new MetaExpression(left, Restrictions);
        }

        public override DynamicMetaObject BindConvert(ConvertBinder binder)
        {
            var binding = PrepareExpression();
            if (binder.Type == typeof(Expression))
            {
                return new DynamicMetaObject(binding, Restrictions);
            }
            else
            {
                binding = Expression.Call(MakeUnaryMethod, ConvertOperator, binding, binder.Type.Const());
                return new MetaExpression(binding, Restrictions);
            }
        }

        public override DynamicMetaObject BindDeleteMember(DeleteMemberBinder binder)
            => throw new NotSupportedException();

        public override DynamicMetaObject BindDeleteIndex(DeleteIndexBinder binder, DynamicMetaObject[] indexes)
            => throw new NotSupportedException();

        public override DynamicMetaObject BindCreateInstance(CreateInstanceBinder binder, DynamicMetaObject[] args)
        {
            var binding = PrepareExpression();
            if (Expression is ConstantExpression expr && expr.Value is ConstantExpression constExpr && constExpr.Value is Type)
                binding = Expression.Call(NewMethod, constExpr, Expression.NewArrayInit(typeof(Expression), args.Select(ToExpression)));
            else
                binding = Expression.Call(ActivateMethod, binding, Expression.NewArrayInit(typeof(Expression), args.Select(ToExpression)));
            return new MetaExpression(binding, Restrictions);
        }
    }
}
