using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Reflection
{
    using static Runtime.CompilerServices.PointerHelpers;

    /// <summary>
    /// Provides access to fast reflection routines.
    /// </summary>
    public static class Reflector
    {
        private static MemberInfo? MemberOf(LambdaExpression exprTree) => exprTree.Body switch
        {
            MemberExpression expr => expr.Member,
            MethodCallExpression expr => expr.Method,
            NewExpression expr => expr.Constructor,
            BinaryExpression expr => expr.Method,
            UnaryExpression expr => expr.Method,
            IndexExpression expr => expr.Indexer,
            _ => null,
        };

        /// <summary>
        /// Extracts member metadata from expression tree.
        /// </summary>
        /// <param name="exprTree">Expression tree.</param>
        /// <typeparam name="TMember">Type of member to reflect.</typeparam>
        /// <returns>Reflected member; or <see langword="null"/>, if lambda expression doesn't reference a member.</returns>
        [Obsolete("Use overloaded generic method that allows to specify delegate type explicitly")]
        public static TMember? MemberOf<TMember>(Expression<Action> exprTree)
            where TMember : MemberInfo => MemberOf<TMember, Action>(exprTree);

        /// <summary>
        /// Extracts member metadata from expression tree.
        /// </summary>
        /// <param name="exprTree">Expression tree.</param>
        /// <typeparam name="TMember">Type of member to reflect.</typeparam>
        /// <typeparam name="TDelegate">The type of lambda expression.</typeparam>
        /// <returns>Reflected member; or <see langword="null"/>, if lambda expression doesn't reference a member.</returns>
        public static TMember? MemberOf<TMember, TDelegate>(Expression<TDelegate> exprTree)
            where TMember : MemberInfo
            where TDelegate : Delegate
            => MemberOf(exprTree) as TMember;

        /// <summary>
        /// Unreflects constructor to its typed and callable representation.
        /// </summary>
        /// <typeparam name="TDelegate">A delegate representing signature of constructor.</typeparam>
        /// <param name="ctor">Constructor to unreflect.</param>
        /// <returns>Unreflected constructor.</returns>
        public static Constructor<TDelegate>? Unreflect<TDelegate>(this ConstructorInfo ctor)
            where TDelegate : MulticastDelegate => Constructor<TDelegate>.GetOrCreate(ctor);

        /// <summary>
        /// Unreflects method to its typed and callable representation.
        /// </summary>
        /// <typeparam name="TDelegate">A delegate representing signature of method.</typeparam>
        /// <param name="method">A method to unreflect.</param>
        /// <returns>Unreflected method.</returns>
        public static Method<TDelegate>? Unreflect<TDelegate>(this MethodInfo method)
            where TDelegate : MulticastDelegate => Method<TDelegate>.GetOrCreate(method);

        /// <summary>
        /// Obtains managed pointer to the static field.
        /// </summary>
        /// <typeparam name="TValue">The field type.</typeparam>
        /// <param name="field">The field to unreflect.</param>
        /// <returns>Unreflected static field.</returns>
        public static Field<TValue> Unreflect<TValue>(this FieldInfo field) => Field<TValue>.GetOrCreate(field);

        /// <summary>
        /// Obtains managed pointer to the instance field.
        /// </summary>
        /// <typeparam name="T">The type of the object that declares instance field.</typeparam>
        /// <typeparam name="TValue">The field type.</typeparam>
        /// <param name="field">The field to unreflect.</param>
        /// <returns>Unreflected instance field.</returns>
        public static Field<T, TValue> Unreflect<T, TValue>(this FieldInfo field)
            where T : notnull => Field<T, TValue>.GetOrCreate(field);

        private static MemberExpression BuildFieldAccess(FieldInfo field, ParameterExpression target)
            => field.IsStatic ? Expression.Field(null, field) : Expression.Field(Expression.Convert(target, field.DeclaringType), field);

        private static Expression BuildGetter(MemberExpression field)
        {
            Expression fieldAccess = field;
            if (fieldAccess.Type.IsPointer)
                fieldAccess = Wrap(fieldAccess);
            if (fieldAccess.Type.IsValueType)
                fieldAccess = Expression.Convert(fieldAccess, typeof(object));
            return fieldAccess;
        }

        private static DynamicInvoker BuildGetter(FieldInfo field)
        {
            var target = Expression.Parameter(typeof(object));
            var arguments = Expression.Parameter(typeof(object[]));
            return Expression.Lambda<DynamicInvoker>(BuildGetter(BuildFieldAccess(field, target)), target, arguments).Compile();
        }

        private static Expression BuildSetter(MemberExpression field, ParameterExpression arguments)
        {
            Expression valueArg = Expression.ArrayIndex(arguments, Expression.Constant(0));
            if (field.Type.IsPointer)
                valueArg = Unwrap(valueArg, field.Type);
            if (valueArg.Type != field.Type)
                valueArg = Expression.Convert(valueArg, field.Type);
            return Expression.Block(typeof(object), Expression.Assign(field, valueArg), Expression.Default(typeof(object)));
        }

        private static DynamicInvoker BuildSetter(FieldInfo field)
        {
            var target = Expression.Parameter(typeof(object));
            var arguments = Expression.Parameter(typeof(object[]));
            return Expression.Lambda<DynamicInvoker>(BuildSetter(BuildFieldAccess(field, target), arguments), target, arguments).Compile();
        }

        private static DynamicInvoker BuildInvoker(FieldInfo field)
        {
            var target = Expression.Parameter(typeof(object));
            var arguments = Expression.Parameter(typeof(object[]));
            var fieldAccess = BuildFieldAccess(field, target);
            var body = Expression.Condition(
                Expression.Equal(Expression.ArrayLength(arguments), Expression.Constant(0)),
                BuildGetter(fieldAccess),
                BuildSetter(fieldAccess, arguments),
                typeof(object));
            return Expression.Lambda<DynamicInvoker>(body, target, arguments).Compile();
        }

        /// <summary>
        /// Creates dynamic invoker for the field.
        /// </summary>
        /// <remarks>
        /// This method doesn't cache the result so the caller is responsible for storing delegate to the field or cache.
        /// <paramref name="flags"/> supports the following combination of values: <see cref="BindingFlags.GetField"/>, <see cref="BindingFlags.SetField"/> or
        /// both.
        /// </remarks>
        /// <param name="field">The field to unreflect.</param>
        /// <param name="flags">Describes the access to the field using invoker.</param>
        /// <returns>The delegate that can be used to access field value.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="flags"/> is invalid.</exception>
        /// <exception cref="NotSupportedException">The type of <paramref name="field"/> is ref-like value type.</exception>
        public static DynamicInvoker Unreflect(this FieldInfo field, BindingFlags flags = BindingFlags.GetField | BindingFlags.SetField)
        {
            if (field.FieldType.IsByRefLike)
                throw new NotSupportedException();
            return flags switch
            {
                BindingFlags.GetField => BuildGetter(field),
                BindingFlags.SetField => BuildSetter(field),
                BindingFlags.GetField | BindingFlags.SetField => BuildInvoker(field),
                _ => throw new ArgumentOutOfRangeException(nameof(flags))
            };
        }

        private static DynamicInvoker Unreflect<TMethod>(TMethod method, Func<Expression?, TMethod, IEnumerable<Expression>, Expression> resultBuilder)
            where TMethod : MethodBase
        {
            var target = Expression.Parameter(typeof(object));
            var arguments = Expression.Parameter(typeof(object[]));
            var thisArg = method.IsStatic || method.MemberType == MemberTypes.Constructor ? null : Expression.Convert(target, method.DeclaringType);
            ICollection<Expression> arglist = new LinkedList<Expression>(), prologue = new LinkedList<Expression>(), epilogue = new LinkedList<Expression>();
            ICollection<ParameterExpression> tempVars = new LinkedList<ParameterExpression>();

            // handle parameters
            foreach (var parameter in method.GetParameters())
            {
                Expression argument = Expression.ArrayAccess(arguments, Expression.Constant(parameter.Position));
                if (parameter.ParameterType.IsByRefLike)
                {
                    throw new NotSupportedException();
                }
                else if (parameter.ParameterType.IsByRef)
                {
                    var parameterType = parameter.ParameterType.GetElementType();
                    var tempVar = Expression.Variable(parameterType);
                    tempVars.Add(tempVar);
                    prologue.Add(Expression.Assign(tempVar, parameterType.IsPointer ? Unwrap(argument, parameterType.GetElementType()) : Expression.Convert(argument, parameterType)));
                    if (parameterType.IsPointer)
                        epilogue.Add(Expression.Assign(argument, Wrap(tempVar)));
                    else if (parameterType.IsValueType)
                        epilogue.Add(Expression.Assign(argument, Expression.Convert(tempVar, typeof(object))));
                    else
                        epilogue.Add(Expression.Assign(argument, tempVar));
                    argument = tempVar;
                }
                else if (parameter.ParameterType.IsPointer)
                {
                    argument = Unwrap(argument, parameter.ParameterType);
                }
                else
                {
                    argument = Expression.Convert(argument, parameter.ParameterType);
                }

                arglist.Add(argument);
            }

            // construct body of the method
            Expression result = resultBuilder(thisArg, method, arglist);
            if (result.Type.IsByRefLike)
                throw new NotSupportedException();
            else if (result.Type == typeof(void))
                epilogue.Add(Expression.Default(typeof(object)));
            if (result.Type.IsPointer)
                result = Wrap(result);
            else if (result.Type.IsValueType)
                result = Expression.Convert(result, typeof(object));

            // construct lambda expression
            bool useTailCall;
            if (epilogue.Count > 0)
            {
                var resultVar = Expression.Variable(typeof(object));
                tempVars.Add(resultVar);
                result = Expression.Assign(resultVar, result);
                epilogue.Add(resultVar);
                result = Expression.Block(typeof(object), tempVars, prologue.Append(result).Concat(epilogue));
                useTailCall = false;
            }
            else
            {
                useTailCall = true;
            }

            // help GC
            arglist.Clear();
            prologue.Clear();
            epilogue.Clear();
            tempVars.Clear();
            return Expression.Lambda<DynamicInvoker>(result, useTailCall, target, arguments).Compile();
        }

        /// <summary>
        /// Creates dynamic invoker for the method.
        /// </summary>
        /// <param name="method">The method to unreflect.</param>
        /// <returns>The delegate that can be used to invoke the method.</returns>
        /// <exception cref="NotSupportedException">The type of parameter or return type is ref-like value type.</exception>
        public static DynamicInvoker Unreflect(this MethodInfo method)
            => Unreflect(method, Expression.Call);

        private static Expression New(Expression? thisArg, ConstructorInfo ctor, IEnumerable<Expression> args)
            => Expression.New(ctor, args);

        /// <summary>
        /// Creates dynamic invoker for the constructor.
        /// </summary>
        /// <param name="ctor">The constructor to unreflect.</param>
        /// <returns>The delegate that can be used to create an object instance.</returns>
        /// <exception cref="NotSupportedException">The type of parameter is ref-like value type.</exception>
        public static DynamicInvoker Unreflect(this ConstructorInfo ctor)
            => Unreflect(ctor, New);
    }
}