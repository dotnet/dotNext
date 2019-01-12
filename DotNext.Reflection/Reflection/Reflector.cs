using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotNext.Reflection
{
    /// <summary>
    /// Provides access to fast reflection routines.
    /// </summary>
    public static class Reflector
    {
        private static class ConstructorCache<D>
            where D: MulticastDelegate
        {
            private static readonly ConditionalWeakTable<ConstructorInfo, Constructor<D>> constructors =
                new ConditionalWeakTable<ConstructorInfo, Constructor<D>>();
            
            internal static Constructor<D> GetOrCreate(ConstructorInfo ctor)
                => constructors.GetValue(ctor, Constructor<D>.Unreflect);
            
        }

        private static class MethodCache<D>
            where D: MulticastDelegate
        {
            private static readonly ConditionalWeakTable<MethodInfo, Method<D>> constructors =
                new ConditionalWeakTable<MethodInfo, Method<D>>();
            
            internal static Method<D> GetOrCreate(MethodInfo ctor)
                => constructors.GetValue(ctor, Method<D>.Unreflect);
        }

        private static class MemberCache<A>
            where A: struct
        {
            private static readonly ConditionalWeakTable<MemberInfo, MemberInvoker<A>> invokers =
                new ConditionalWeakTable<MemberInfo, MemberInvoker<A>>();

            private static MemberInvoker<A> Create(MethodInfo method)
            {
                var methodParams = method.GetParameterTypes();
                var (_, arglist, input) = Signature.Reflect<A>();
                var postExpressions = new LinkedList<Expression>();
                var locals = new LinkedList<ParameterExpression>();
                Expression returnArg;
                Expression thisArg;
                
                if(method.IsStatic)
                    //all fields of struct are arguments
                    if(method.ReturnType == typeof(void))
                        if(methodParams.LongLength == arglist.LongLength)
                        {
                            thisArg = null;
                            returnArg = null;
                        }
                        else
                            return null;
                    //last field is a method return type
                    else if(methodParams.LongLength == arglist.LongLength -1)
                    {
                        thisArg = null;
                        returnArg = arglist[arglist.LongLength - 1];
                        arglist = arglist.RemoveLast(1);
                    }
                    else
                        return null;
                //first field is an instance, all other - arguments
                else 
                    if(method.ReturnType == typeof(void))
                        if(methodParams.LongLength == arglist.LongLength -1)
                        {
                            returnArg = null;
                            thisArg = arglist[0];
                            arglist = arglist.RemoveFirst(1);
                        }
                        else
                            return null;
                    else 
                        if(methodParams.LongLength == arglist.LongLength - 2)
                        {
                            returnArg = arglist[arglist.LongLength - 1];
                            thisArg = arglist[0];
                            arglist = arglist.Slice(1, arglist.LongLength - 1);
                        }
                        else
                            return null;
                if(!NormalizeParameters(methodParams, arglist, locals, postExpressions))
                    return null;
                else if(!(thisArg is null))
                    thisArg = NormalizeParameter(method.DeclaringType, thisArg, out _, out _);
                Expression body = Expression.Call(thisArg, method, arglist);
                if(!(returnArg is null))
                    body = returnArg.Type == body.Type ?
                        Expression.Assign(returnArg, body) :
                        Expression.Assign(returnArg, Expression.Convert(body, returnArg.Type));
                postExpressions.AddFirst(body);
                body = postExpressions.Count == 1 ? postExpressions.First.Value : Expression.Block(locals, postExpressions);
                return Expression.Lambda<MemberInvoker<A>>(body, input).Compile();
            }

            private static MemberInvoker<A> Create(MemberInfo member)
            {
                switch(member)
                {
                    case MethodInfo method:
                        return Create(method);
                    default:
                        return null;
                }
            }

            internal static MemberInvoker<A> GetOrCreate(MethodInfo method)
                => invokers.GetValue(method, Create);
        }

        /// <summary>
        /// Extracts member metadata from expression tree.
        /// </summary>
        /// <param name="exprTree">Expression tree.</param>
        /// <typeparam name="M">Type of member to reflect.</typeparam>
        /// <returns>Reflected member; or null, if lambda expression doesn't reference a member.</returns>
        public static M MemberOf<M>(Expression<Action> exprTree)
            where M: MemberInfo
        {
            if(exprTree.Body is MemberExpression member)
                return member.Member as M;
            else if(exprTree.Body is MethodCallExpression method)
                return method.Method as M;
            else if(exprTree.Body is NewExpression ctor)
                return ctor.Constructor as M;
            else
                return null;
        }

		/// <summary>
		/// Unreflects constructor to its typed and callable representation.
		/// </summary>
		/// <typeparam name="D">A delegate representing signature of constructor.</typeparam>
		/// <param name="ctor">Constructor to unreflect.</param>
		/// <returns>Unreflected constructor.</returns>
		public static Constructor<D> Unreflect<D>(this ConstructorInfo ctor)
            where D: MulticastDelegate
            => ConstructorCache<D>.GetOrCreate(ctor);

		/// <summary>
		/// Unreflects method to its typed and callable representation.
		/// </summary>
		/// <typeparam name="D">A delegate representing signature of method.</typeparam>
		/// <param name="method">A method to unreflect.</param>
		/// <returns>Unreflected method.</returns>
		public static Method<D> Unreflect<D>(this MethodInfo method)
            where D: MulticastDelegate
            => MethodCache<D>.GetOrCreate(method);

        private static Expression NormalizeParameter(Type actualParameter, Expression expectedParameter, out ParameterExpression localVar, out Expression postExpression)
        {
            if(expectedParameter.Type == actualParameter)
            {
                postExpression = localVar = null;
                return expectedParameter;
            }
            else if(expectedParameter.Type == typeof(object))
                if(actualParameter.IsByRef)
                {
                    //T local = args.param is null ? default(T) : (T)args;
                    //...call(ref local)
                    //args.param = (object)local;
                    localVar = Expression.Variable(actualParameter.GetElementType());
                    postExpression = localVar.Type.IsValueType ?
                        Expression.Assign(expectedParameter, Expression.Convert(localVar, expectedParameter.Type)):
                        Expression.Assign(expectedParameter, localVar);
                    postExpression = Expression.Assign(expectedParameter, Expression.Convert(localVar, expectedParameter.Type));
                    return Expression.Assign(localVar, Expression.Condition(Expression.ReferenceEqual(expectedParameter, Expression.Constant(null, expectedParameter.Type)), 
                        Expression.Default(actualParameter.GetElementType()),
                        Expression.Convert(expectedParameter, actualParameter.GetElementType())));
                }
                else
                {
                    postExpression = localVar = null;
                    return Expression.Condition(Expression.ReferenceEqual(expectedParameter, Expression.Constant(null, expectedParameter.Type)), 
                        Expression.Default(actualParameter),
                        Expression.Convert(expectedParameter, actualParameter));
                }
            else if(actualParameter.IsByRef)
                {
                    postExpression = localVar = null;
                    return expectedParameter;
                }
            else 
            {
                postExpression = localVar = null;
                return Expression.Convert(expectedParameter, actualParameter);
            }
        }

        private static bool NormalizeParameters(Type[] actualParameters, Expression[] expectedParameters, ICollection<ParameterExpression> locals, ICollection<Expression> postExpressions)
        {
            for(var i = 0L; i < actualParameters.LongLength; i++)
                if((expectedParameters[i] = NormalizeParameter(actualParameters[i], expectedParameters[i], out var localVar, out var postExpr)) is null)
                    return false;
                else if(!(postExpr is null) && !(localVar is null))
                {
                    locals.Add(localVar);
                    postExpressions.Add(postExpr);
                }
            return true;
        }
        
        /// <summary>
        /// Returns fast invoker for the specified method.
        /// </summary>
        /// <param name="method">A method to wrap into invoker.</param>
        /// <typeparam name="A">A structure describing arguments including hidden <see langword="this"/> parameter and return type.</typeparam>
        /// <returns>A delegate that can be used to invoke method directly, without .NET Reflection.</returns>
        public static MemberInvoker<A> AsInvoker<A>(this MethodInfo method)
            where A: struct
            => MemberCache<A>.GetOrCreate(method);

        /// <summary>
        /// Allocates blank invocation arguments.
        /// </summary>
        /// <param name="invoker">Member invoker.</param>
        /// <typeparam name="A">Type of member invocation arguments to allocate.</typeparam>
        /// <returns>Allocated arguments on the stack.</returns>
        public static A ArgList<A>(this MemberInvoker<A> invoker)
            where A: struct
            => new A();
    }
}