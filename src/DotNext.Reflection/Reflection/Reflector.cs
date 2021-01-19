using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Reflection
{
    /// <summary>
    /// Provides access to fast reflection routines.
    /// </summary>
    public static partial class Reflector
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
        /// <param name="volatileAccess"><see langword="true"/> to generate volatile access to the field.</param>
        /// <returns>The delegate that can be used to access field value.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="flags"/> is invalid.</exception>
        /// <exception cref="NotSupportedException">The type of <paramref name="field"/> is ref-like value type.</exception>
        public static DynamicInvoker Unreflect(this FieldInfo field, BindingFlags flags = BindingFlags.GetField | BindingFlags.SetField, bool volatileAccess = false)
        {
            if (field.FieldType.IsByRefLike)
                throw new NotSupportedException();

            return flags switch
            {
                BindingFlags.GetField => BuildFieldGetter(field, volatileAccess),
                BindingFlags.SetField => BuildFieldSetter(field, volatileAccess),
                BindingFlags.GetField | BindingFlags.SetField => BuildFieldAccess(field, volatileAccess),
                _ => throw new ArgumentOutOfRangeException(nameof(flags))
            };
        }

        /// <summary>
        /// Creates dynamic invoker for the method.
        /// </summary>
        /// <param name="method">The method to unreflect.</param>
        /// <returns>The delegate that can be used to invoke the method.</returns>
        /// <exception cref="NotSupportedException">The type of parameter or return type is ref-like value type.</exception>
        public static unsafe DynamicInvoker Unreflect(this MethodInfo method)
            => Unreflect(method, &Expression.Call);

        /// <summary>
        /// Creates dynamic invoker for the constructor.
        /// </summary>
        /// <param name="ctor">The constructor to unreflect.</param>
        /// <returns>The delegate that can be used to create an object instance.</returns>
        /// <exception cref="NotSupportedException">The type of parameter is ref-like value type.</exception>
        public static unsafe DynamicInvoker Unreflect(this ConstructorInfo ctor)
        {
            return Unreflect(ctor, &New);

            static Expression New(Expression? thisArg, ConstructorInfo ctor, IEnumerable<Expression> args)
                => Expression.New(ctor, args);
        }
    }
}