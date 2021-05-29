using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace DotNext.VariantType
{
    internal sealed class VariantMetaObject : DynamicMetaObject
    {
        private readonly Expression valueAccess;

        internal VariantMetaObject(Expression parameter, IVariant variant)
            : base(parameter, BindingRestrictions.Empty, variant)
        {
            valueAccess = Expression.Property(Expression.TypeAs(parameter, typeof(IVariant)), typeof(IVariant), nameof(IVariant.Value));
        }

        private BindingRestrictions CreateRestrictions()
            => BindingRestrictions.GetExpressionRestriction(Expression.TypeIs(Expression, typeof(IVariant)));

        private DynamicMetaObject GetValueObject()
            => new DynamicMetaObject(valueAccess, BindingRestrictions.Empty, Unsafe.As<IVariant>(Value)?.Value!);

        private DynamicMetaObject ApplyRestrictions(DynamicMetaObject value)
            => new DynamicMetaObject(value.Expression, CreateRestrictions().Merge(value.Restrictions));

        public sealed override DynamicMetaObject BindBinaryOperation(BinaryOperationBinder binder, DynamicMetaObject arg)
            => ApplyRestrictions(binder.FallbackBinaryOperation(GetValueObject(), arg));

        public sealed override DynamicMetaObject BindConvert(ConvertBinder binder)
            => ApplyRestrictions(binder.FallbackConvert(GetValueObject()));

        public sealed override DynamicMetaObject BindCreateInstance(CreateInstanceBinder binder, DynamicMetaObject[] args)
            => ApplyRestrictions(binder.FallbackCreateInstance(GetValueObject(), args));

        public sealed override DynamicMetaObject BindDeleteIndex(DeleteIndexBinder binder, DynamicMetaObject[] indexes)
            => ApplyRestrictions(binder.FallbackDeleteIndex(GetValueObject(), indexes));

        public sealed override DynamicMetaObject BindDeleteMember(DeleteMemberBinder binder)
            => ApplyRestrictions(binder.FallbackDeleteMember(GetValueObject()));

        public sealed override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
            => ApplyRestrictions(binder.FallbackGetIndex(GetValueObject(), indexes));

        public sealed override DynamicMetaObject BindGetMember(GetMemberBinder binder)
            => ApplyRestrictions(binder.FallbackGetMember(GetValueObject()));

        public sealed override DynamicMetaObject BindInvoke(InvokeBinder binder, DynamicMetaObject[] args)
            => ApplyRestrictions(binder.FallbackInvoke(GetValueObject(), args));

        public sealed override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
            => ApplyRestrictions(binder.FallbackInvokeMember(GetValueObject(), args));

        public sealed override DynamicMetaObject BindSetIndex(SetIndexBinder binder, DynamicMetaObject[] indexes, DynamicMetaObject value)
            => ApplyRestrictions(binder.FallbackSetIndex(GetValueObject(), indexes, value));

        public sealed override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
            => ApplyRestrictions(binder.FallbackSetMember(GetValueObject(), value));

        public sealed override DynamicMetaObject BindUnaryOperation(UnaryOperationBinder binder)
            => ApplyRestrictions(binder.FallbackUnaryOperation(GetValueObject()));

        public sealed override IEnumerable<string> GetDynamicMemberNames() => Array.Empty<string>();
    }
}
