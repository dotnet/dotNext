using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Reflection
{
    /// <summary>
    /// Provides access to fast reflection routines.
    /// </summary>
    public static class Reflector
    {
        /// <summary>
        /// Extracts member metadata from expression tree.
        /// </summary>
        /// <param name="exprTree">Expression tree.</param>
        /// <typeparam name="M">Type of member to reflect.</typeparam>
        /// <returns>Reflected member; or null, if lambda expression doesn't reference a member.</returns>
        public static M? MemberOf<M>(Expression<Action> exprTree)
            where M : MemberInfo
            => exprTree.Body switch
            {
                MemberExpression expr => expr.Member as M,
                MethodCallExpression expr => expr.Method as M,
                NewExpression expr => expr.Constructor as M,
                BinaryExpression expr => expr.Method as M,
                UnaryExpression expr => expr.Method as M,
                IndexExpression expr => expr.Indexer as M,
                _ => null,
            };

        /// <summary>
        /// Unreflects constructor to its typed and callable representation.
        /// </summary>
        /// <typeparam name="D">A delegate representing signature of constructor.</typeparam>
        /// <param name="ctor">Constructor to unreflect.</param>
        /// <returns>Unreflected constructor.</returns>
        public static Constructor<D>? Unreflect<D>(this ConstructorInfo ctor) where D : MulticastDelegate => Constructor<D>.GetOrCreate(ctor);

        /// <summary>
        /// Unreflects method to its typed and callable representation.
        /// </summary>
        /// <typeparam name="D">A delegate representing signature of method.</typeparam>
        /// <param name="method">A method to unreflect.</param>
        /// <returns>Unreflected method.</returns>
        public static Method<D>? Unreflect<D>(this MethodInfo method) where D : MulticastDelegate => Method<D>.GetOrCreate(method);

        /// <summary>
        /// Obtains managed pointer to the static field.
        /// </summary>
        /// <typeparam name="V">The field type.</typeparam>
        /// <param name="field">The field to unreflect.</param>
        /// <returns>The managed pointer to the field.</returns>
        [return: MaybeNull]
        public static ref V Unreflect<V>(this FieldInfo field) => ref Field<V>.GetOrCreate(field).Value!;

        /// <summary>
        /// Obtains managed pointer to the instance field.
        /// </summary>
        /// <typeparam name="T">The type of the object that declares instance field.</typeparam>
        /// <typeparam name="V">The field type.</typeparam>
        /// <param name="field">The field to unreflect.</param>
        /// <param name="instance">The object that contains instance field.</param>
        /// <returns>The managed pointer to the field.</returns>
        [return: MaybeNull]
        public static ref V Unreflect<T, V>(this FieldInfo field, [DisallowNull]in T instance) => ref Field<T, V>.GetOrCreate(field)[instance]!;
    }
}