using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNext
{
    using Reflection;
    using Runtime.CompilerServices;
    using Runtime.InteropServices;

    /// <summary>
    /// Generates hash code and equality check functions for the particular type.
    /// </summary>
    /// <remarks>
    /// Automatically generated hash code and equality check functions can be used
    /// instead of manually written implementation of overridden <see cref="object.GetHashCode"/> and <see cref="object.Equals(object)"/> methods.
    /// </remarks>
    [RuntimeFeatures(RuntimeGenericInstantiation = true, DynamicCodeCompilation = true, PrivateReflection = true)]
    public static class EqualityComparerBuilder
    {
        private sealed class DynamicEqualityComparer<T> : IEqualityComparer<T>
        {
            private readonly Func<T, T, bool> equality;
            private readonly Func<T, int> hashCode;

            internal DynamicEqualityComparer()
            {
                equality = BuildEquals<T>();
                hashCode = BuildGetHashCode<T>();
            }

            bool IEqualityComparer<T>.Equals(T x, T y) => equality(x, y);

            int IEqualityComparer<T>.GetHashCode(T obj) => hashCode(obj);
        }

        private static MethodInfo EqualsMethodForValueType(Type type)
            => typeof(ValueType<>).MakeGenericType(type).GetMethod(nameof(ValueType<int>.BitwiseEquals), new[] { type, type });

        private static MethodInfo HashCodeMethodForValueType(Type type)
            => typeof(ValueType<>).MakeGenericType(type).GetMethod(nameof(ValueType<int>.BitwiseHashCode), new[] { type, typeof(bool) });

        private static MethodInfo EqualsMethodForArrayElementType(Type itemType)
            => itemType.IsValueType ?
                    typeof(OneDimensionalArray)
                        .GetMethod(nameof(OneDimensionalArray.BitwiseEquals), BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly, 1, new Type[] { null, null })
                        .MakeGenericMethod(itemType) :
                        typeof(OneDimensionalArray)
                        .GetMethod(nameof(OneDimensionalArray.SequenceEqual), new[] { typeof(object[]), typeof(object[]) });

        private static MethodInfo HashCodeMethodForArrayElementType(Type itemType)
            => itemType.IsValueType ?
                typeof(OneDimensionalArray)
                        .GetMethod(nameof(OneDimensionalArray.BitwiseHashCode), BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly, 1, new Type[] { null, typeof(bool) })
                        .MakeGenericMethod(itemType) :
                typeof(Sequence)
                        .GetMethod(nameof(Sequence.SequenceHashCode), new[] { typeof(IEnumerable<object>), typeof(bool) });

        private static IEnumerable<FieldInfo> GetAllFields(this Type type)
        {
            foreach (var t in type.GetBaseTypes(includeTopLevel: true, includeInterfaces: false))
                foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic))
                    yield return field;
        }

        /// <summary>
        /// Returns automatically generated equality check function for the type <typeparamref name="T"/>.
        /// </summary>
        /// <remarks>
        /// It is recommended to cache the returned value into field and reuse it for every call of <see cref="object.Equals(object)"/>
        /// because generation process is expensive.
        /// </remarks>
        /// <typeparam name="T">The type for which equality check function should be generated.</typeparam>
        /// <returns>Equality check function for the type <typeparamref name="T"/>.</returns>
        public static Func<T, T, bool> BuildEquals<T>()
        {
            var type = typeof(T);
            if (type.IsPrimitive)
                return EqualityComparer<T>.Default.Equals;
            else if (type.IsValueType)
                return EqualsMethodForValueType(type).CreateDelegate<Func<T, T, bool>>();
            else if (type.IsArray && type.GetArrayRank() == 1)
                return EqualsMethodForArrayElementType(type.GetElementType()).CreateDelegate<Func<T, T, bool>>();
            else if (type.IsClass)
            {
                var x = Expression.Parameter(type);
                var y = Expression.Parameter(type);
                //collect all fields in the hierachy
                Expression expr = Expression.ReferenceNotEqual(y, Expression.Constant(null, y.Type));
                foreach (var field in type.GetAllFields())
                {
                    var fieldX = Expression.Field(x, field);
                    var fieldY = Expression.Field(y, field);
                    Expression condition;
                    if (field.FieldType.IsPointer || field.FieldType.IsPrimitive || field.FieldType.IsEnum)
                        condition = Expression.Equal(fieldX, fieldY);
                    else if (field.FieldType.IsValueType)
                        condition = Expression.Call(EqualsMethodForValueType(field.FieldType), fieldX, fieldY);
                    else if (field.FieldType.IsArray && field.FieldType.GetArrayRank() == 1)
                        condition = Expression.Call(EqualsMethodForArrayElementType(field.FieldType.GetElementType()), fieldX, fieldY);
                    else
                        condition = Expression.Call(typeof(object).GetMethod(nameof(Equals), new[] { typeof(object), typeof(object) }), fieldX, fieldY);
                    expr = Expression.AndAlso(expr, condition);
                }
                expr = Expression.OrElse(Expression.ReferenceEqual(x, y), expr);
                return Expression.Lambda<Func<T, T, bool>>(expr, false, x, y).Compile();
            }
            else
                return EqualityComparer<T>.Default.Equals;
        }

        /// <summary>
        /// Returns automatically generated hash code function for the type <typeparamref name="T"/>.
        /// </summary>
        /// <remarks>
        /// It is recommended to cache the returned value into field and reuse it for every call of <see cref="object.GetHashCode()"/>
        /// because generation process is expensive.
        /// </remarks>
        /// <typeparam name="T">The type for which hash code function should be generated.</typeparam>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Equality check function for the type <typeparamref name="T"/>.</returns>
        public static Func<T, int> BuildGetHashCode<T>(bool salted = true)
        {
            var type = typeof(T);
            Expression expr;
            var inputParam = Expression.Parameter(type);
            if (type.IsPrimitive)
                return EqualityComparer<T>.Default.GetHashCode;
            else if (type.IsValueType)
            {
                expr = Expression.Call(HashCodeMethodForValueType(type), inputParam, Expression.Constant(salted));
                return Expression.Lambda<Func<T, int>>(expr, true, inputParam).Compile();
            }
            else if (type.IsArray && type.GetArrayRank() == 1)
            {
                expr = Expression.Call(HashCodeMethodForArrayElementType(type.GetElementType()), inputParam, Expression.Constant(salted));
                return Expression.Lambda<Func<T, int>>(expr, true, inputParam).Compile();
            }
            else if (type.IsClass)
            {
                var hashCodeTemp = Expression.Parameter(typeof(int));
                ICollection<Expression> expressions = new LinkedList<Expression>();
                //collect all fields in the hierachy
                foreach (var field in type.GetAllFields())
                {
                    expr = Expression.Field(inputParam, field);
                    if (field.FieldType.IsPointer)
                        expr = Expression.Call(typeof(Memory).GetMethod(nameof(Memory.PointerHashCode), BindingFlags.Static | BindingFlags.DeclaredOnly | BindingFlags.NonPublic), expr);
                    else if (field.FieldType.IsPrimitive)
                        expr = Expression.Call(expr, nameof(GetHashCode), Array.Empty<Type>());
                    else if (field.FieldType.IsValueType)
                        expr = Expression.Call(HashCodeMethodForValueType(field.FieldType), expr, Expression.Constant(salted));
                    else if (field.FieldType.IsArray && field.FieldType.GetArrayRank() == 1)
                        expr = Expression.Call(HashCodeMethodForArrayElementType(field.FieldType.GetElementType()), expr, Expression.Constant(salted));
                    else
                    {
                        expr = Expression.Condition(
                            Expression.ReferenceEqual(expr, Expression.Constant(null, expr.Type)),
                            Expression.Constant(0, typeof(int)),
                            Expression.Call(expr, nameof(GetHashCode), Array.Empty<Type>()));
                    }
                    expr = Expression.Assign(hashCodeTemp, Expression.Add(Expression.Multiply(hashCodeTemp, Expression.Constant(-1521134295)), expr));
                    expressions.Add(expr);
                }
                expressions.Add(hashCodeTemp);
                expr = Expression.Block(typeof(int), Sequence.Singleton(hashCodeTemp), expressions);
                return Expression.Lambda<Func<T, int>>(expr, false, inputParam).Compile();
            }
            else
                return EqualityComparer<T>.Default.GetHashCode;
        }

        /// <summary>
        /// Generates implementation of equality comparer.
        /// </summary>
        /// <typeparam name="T">The type for which equality comparer should be generated.</typeparam>
        /// <returns>The generated equality comparer.</returns>
        public static IEqualityComparer<T> Build<T>() => new DynamicEqualityComparer<T>();
    }
}