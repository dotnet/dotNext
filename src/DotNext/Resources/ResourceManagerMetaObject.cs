using System;
using System.Dynamic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using static InlineIL.IL;
using static InlineIL.IL.Emit;
using static InlineIL.MethodRef;
using static InlineIL.TypeRef;

namespace DotNext.Resources
{
    internal sealed class ResourceManagerMetaObject : DynamicMetaObject
    {
        internal ResourceManagerMetaObject(Expression parameter, ResourceManager manager)
            : base(parameter, BindingRestrictions.GetExpressionRestriction(Expression.TypeIs(parameter, typeof(ResourceManager))), manager)
        {
        }

        private new ResourceManager Value => Unsafe.As<ResourceManager>(base.Value);

        private static MethodInfo GetStringMethod
        {
            get
            {
                Ldtoken(Method(Type<ResourceManager>(), nameof(ResourceManager.GetString), Type<string>()));
                Pop(out RuntimeMethodHandle handle);
                return (MethodInfo)MethodBase.GetMethodFromHandle(handle);
            }
        }

        private static MethodInfo GetStreamMethod
        {
            get
            {
                Ldtoken(Method(Type<ResourceManager>(), nameof(ResourceManager.GetStream), Type<string>()));
                Pop(out RuntimeMethodHandle handle);
                return (MethodInfo)MethodBase.GetMethodFromHandle(handle);
            }
        }

        private static MethodInfo GetObjectMethod
        {
            get
            {
                Ldtoken(Method(Type<ResourceManager>(), nameof(ResourceManager.GetObject), Type<string>()));
                Pop(out RuntimeMethodHandle handle);
                return (MethodInfo)MethodBase.GetMethodFromHandle(handle);
            }
        }

        private static MethodInfo FormatStringMethod
        {
            get
            {
                Ldtoken(Method(Type<string>(), nameof(string.Format), Type<string>(), Type<object[]>()));
                Pop(out RuntimeMethodHandle handle);
                return (MethodInfo)MethodBase.GetMethodFromHandle(handle);
            }
        }

        private static MethodInfo ConvertToStringMethod
        {
            get
            {
                Ldtoken(Method(typeof(Convert), nameof(Convert.ToString), Type<object>(), Type<IFormatProvider>()));
                Pop(out RuntimeMethodHandle handle);
                return (MethodInfo)MethodBase.GetMethodFromHandle(handle);
            }
        }

        private DynamicMetaObject BindResource(Type returnType, Expression resourceName, BindingRestrictions? restrictions = null)
        {
            MethodInfo method;
            if (returnType == typeof(string))
                method = GetStringMethod;
            else if (returnType == typeof(Stream))
                method = GetStreamMethod;
            else
                method = GetObjectMethod;

            return new DynamicMetaObject(
                Expression.Call(Expression.Convert(Expression, method.DeclaringType), method, resourceName),
                restrictions is null ? Restrictions : Restrictions.Merge(restrictions));
        }

        public override DynamicMetaObject BindGetMember(GetMemberBinder binder)
            => BindResource(binder.ReturnType, Expression.Constant(binder.Name));

        private DynamicMetaObject UnsupportedBinding
            => new DynamicMetaObject(Expression.Throw(Expression.New(typeof(NotSupportedException)), typeof(object)), Restrictions);

        public override DynamicMetaObject BindBinaryOperation(BinaryOperationBinder binder, DynamicMetaObject arg)
            => UnsupportedBinding;

        public sealed override DynamicMetaObject BindCreateInstance(CreateInstanceBinder binder, DynamicMetaObject[] args)
            => UnsupportedBinding;

        public sealed override DynamicMetaObject BindDeleteIndex(DeleteIndexBinder binder, DynamicMetaObject[] indexes)
            => UnsupportedBinding;

        public sealed override DynamicMetaObject BindDeleteMember(DeleteMemberBinder binder)
            => UnsupportedBinding;

        public sealed override DynamicMetaObject BindGetIndex(GetIndexBinder binder, DynamicMetaObject[] indexes)
        {
            if (indexes.LongLength != 1)
                return UnsupportedBinding;

            return BindResource(binder.ReturnType, Expression.Call(null, ConvertToStringMethod, indexes[0].Expression, Expression.Constant(null, typeof(IFormatProvider))));
        }

        public sealed override DynamicMetaObject BindInvoke(InvokeBinder binder, DynamicMetaObject[] args)
            => UnsupportedBinding;

        public sealed override DynamicMetaObject BindInvokeMember(InvokeMemberBinder binder, DynamicMetaObject[] args)
        {
            var method = GetStringMethod;
            Expression expr = Expression.Call(Expression.Convert(Expression, method.DeclaringType), method, Expression.Constant(binder.Name));

            var constructedArgs = new Expression[args.LongLength];
            for (var i = 0L; i < args.LongLength; i++)
                constructedArgs[i] = args[i].Expression;

            expr = Expression.Call(null, FormatStringMethod, expr, Expression.NewArrayInit(typeof(object), constructedArgs));

            return new DynamicMetaObject(expr, Restrictions);
        }

        public sealed override DynamicMetaObject BindSetIndex(SetIndexBinder binder, DynamicMetaObject[] indexes, DynamicMetaObject value)
            => UnsupportedBinding;

        public sealed override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value)
            => UnsupportedBinding;

        public sealed override DynamicMetaObject BindUnaryOperation(UnaryOperationBinder binder)
            => UnsupportedBinding;
    }
}