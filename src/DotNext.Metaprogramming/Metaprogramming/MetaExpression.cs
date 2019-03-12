using System;
using System.Reflection;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    internal sealed class MetaExpression : DynamicMetaObject
    {
        private static readonly MethodInfo MakeUnaryMethod = typeof(Expression).GetMethod(nameof(Expression.MakeUnary), new[] { typeof(ExpressionType), typeof(Expression), typeof(Type) });
        private static readonly MethodInfo MakeBinaryMethod = typeof(Expression).GetMethod(nameof(Expression.MakeBinary), new[] { typeof(ExpressionType), typeof(Expression), typeof(Expression) });
        private static readonly MethodInfo PropertyOrFieldMethod = typeof(Expression).GetMethod(nameof(Expression.PropertyOrField), new[] { typeof(Expression), typeof(string) });
        private static readonly MethodInfo AssignMethod = typeof(Expression).GetMethod(nameof(Expression.Assign), new[] { typeof(Expression), typeof(Expression) });
        private static readonly MethodInfo CallMethod = typeof(ExpressionBuilder).GetMethod(nameof(ExpressionBuilder.Call), new[] { typeof(Expression), typeof(string), typeof(Expression[]) });
        private static readonly MethodInfo InvokeMethod = typeof(Expression).GetMethod(nameof(Expression.Invoke), new[] { typeof(Expression), typeof(Expression[]) });
        private static readonly MethodInfo NewMethod = typeof(ExpressionBuilder).GetMethod(nameof(ExpressionBuilder.New), new[] { typeof(Type), typeof(Expression[]) });
        private static readonly MethodInfo PropertyMethod = typeof(Expression).GetMethod(nameof(Expression.Property), new[] { typeof(Expression), typeof(string), typeof(Expression[]) });
        private static readonly MethodInfo ActivateMethod = typeof(ExpressionBuilder).GetMethod(nameof(ExpressionBuilder.New), new[] { typeof(Expression), typeof(Expression[]) });

        private static readonly ConstantExpression ItemName = "Item".AsConst();
        private static readonly ConstantExpression ConvertOperator = ExpressionType.Convert.AsConst();

        internal MetaExpression(Expression binding, IExpressionBuilder<Expression> builder)
            : base(binding, BindingRestrictions.GetTypeRestriction(binding, builder.GetType()), builder)
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
            else if (typeof(Expression).IsAssignableFrom(arg.LimitType))
                return arg.Expression;
            else
                return Expression.Constant(arg.Value, arg.LimitType).AsConst();
        }

        private Expression PrepareExpression()
            => Value is IExpressionBuilder<Expression> builder ? Expression.Convert<IExpressionBuilder<Expression>>().Call(nameof(IExpressionBuilder<Expression>.Build)) : Expression;

        public override DynamicMetaObject BindUnaryOperation(UnaryOperationBinder binder)
        {
            var binding = PrepareExpression();
            binding = Expression.Call(MakeUnaryMethod, binder.Operation.AsConst(), binding, default(Type).AsConst());
            return new MetaExpression(binding, Restrictions);
        }

        public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
        {
            var binding = PrepareExpression();
            binding = Expression.Call(PropertyOrFieldMethod, binding, binder.Name.AsConst());
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
            binding = Expression.Call(CallMethod, binding, binder.Name.AsConst(), Expression.NewArrayInit(typeof(Expression), args.Select(ToExpression)));
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
            binding = Expression.Call(PropertyOrFieldMethod, binding, binder.Name.AsConst());
            return new MetaExpression(binding, Restrictions);
        }

        public override DynamicMetaObject BindBinaryOperation(BinaryOperationBinder binder, DynamicMetaObject arg)
        {
            var left = PrepareExpression();
            left = Expression.Call(MakeBinaryMethod, binder.Operation.AsConst(), left, ToExpression(arg));
            return new MetaExpression(left, Restrictions);
        }

        public override DynamicMetaObject BindConvert(ConvertBinder binder)
        {
            var binding = PrepareExpression();
            if (binder.Type == typeof(Expression))
                return new DynamicMetaObject(binding, Restrictions);
            else if (binder.Type == typeof(UniversalExpression))
                return new DynamicMetaObject(typeof(UniversalExpression).New(binding), Restrictions);
            else
            {
                binding = Expression.Call(MakeUnaryMethod, ConvertOperator, binding, binder.Type.AsConst());
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
