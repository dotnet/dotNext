using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    /// <typeparam name="T">The type for which equality check and hash code functions should be generated.</typeparam>
    /// <remarks>
    /// Automatically generated hash code and equality check functions can be used
    /// instead of manually written implementation of overridden <see cref="object.GetHashCode"/> and <see cref="object.Equals(object)"/> methods.
    /// </remarks>
    [RuntimeFeatures(RuntimeGenericInstantiation = true, DynamicCodeCompilation = true, PrivateReflection = true)]
    public struct EqualityComparerBuilder<T>
    {
        private bool salted;
        private ICollection<string> excludedFields;

        /// <summary>
        /// Sets an array of excluded field names.
        /// </summary>
        /// <value>An array of excluded fields.</value>
        [SuppressMessage("Performance", "CA1819", Justification = "Property is write-only")]
        public string[] ExcludedFields
        {
            set => excludedFields = new HashSet<string>(value);
        }

        private bool IsIncluded(FieldInfo field) => excludedFields is null || !excludedFields.Contains(field.Name);

        /// <summary>
        /// Set a value indicating that hash code must be unique for each application instance.
        /// </summary>
        /// <value><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</value>
        public bool SaltedHashCode
        {
            set => salted = value;
        }

        private readonly struct ConstructedEqualityComparer : IEqualityComparer<T>
        {
            private readonly Func<T, T, bool> equality;
            private readonly Func<T, int> hashCode;

            internal ConstructedEqualityComparer(Func<T, T, bool> equality, Func<T, int> hashCode)
            {
                this.equality = equality;
                this.hashCode = hashCode;
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

        private static IEnumerable<FieldInfo> GetAllFields(Type type)
        {
            foreach (var t in type.GetBaseTypes(includeTopLevel: true, includeInterfaces: false))
                foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic))
                    yield return field;
        }

        private Func<T, T, bool> BuildEquals()
        {
            var type = typeof(T);
            if (type.IsPrimitive)
                return EqualityComparer<T>.Default.Equals;
            else if (type.IsArray && type.GetArrayRank() == 1)
                return EqualsMethodForArrayElementType(type.GetElementType()).CreateDelegate<Func<T, T, bool>>();
            else
            {
                var x = Expression.Parameter(type);
                var y = Expression.Parameter(type);
                //collect all fields in the hierachy
                Expression expr = type.IsClass ? Expression.ReferenceNotEqual(y, Expression.Constant(null, y.Type)) : null;
                foreach (var field in GetAllFields(type))
                    if(IsIncluded(field))
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
                        expr = expr is null ? condition : Expression.AndAlso(expr, condition);
                    }
                if(type.IsClass)
                    expr = Expression.OrElse(Expression.ReferenceEqual(x, y), expr);
                return Expression.Lambda<Func<T, T, bool>>(expr, false, x, y).Compile();
            }
        }

        private Func<T, int> BuildGetHashCode()
        {
            var type = typeof(T);
            Expression expr;
            var inputParam = Expression.Parameter(type);
            if (type.IsPrimitive)
                return EqualityComparer<T>.Default.GetHashCode;
            else if (type.IsArray && type.GetArrayRank() == 1)
            {
                expr = Expression.Call(HashCodeMethodForArrayElementType(type.GetElementType()), inputParam, Expression.Constant(salted));
                return Expression.Lambda<Func<T, int>>(expr, true, inputParam).Compile();
            }
            else
            {
                var hashCodeTemp = Expression.Parameter(typeof(int));
                ICollection<Expression> expressions = new LinkedList<Expression>();
                //collect all fields in the hierachy
                foreach (var field in GetAllFields(type))
                    if(IsIncluded(field))
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
        }

        /// <summary>
        /// Generates implementation of <see cref="object.GetHashCode"/> and <see cref="object.Equals(object)"/> methods
        /// for particular type <typeparamref name="T"/>.
        /// </summary>
        /// <param name="equals">The implementation of equality check.</param>
        /// <param name="hashCode">The implementation of hash code.</param>
        public void Build(out Func<T, T, bool> equals, out Func<T, int> hashCode)
        {
            equals = BuildEquals();
            hashCode = BuildGetHashCode();
        }

        /// <summary>
        /// Generates implementation of equality comparer.
        /// </summary>
        /// <returns>The generated equality comparer.</returns>
        public IEqualityComparer<T> Build() 
            => typeof(T).IsPrimitive ? (IEqualityComparer<T>)EqualityComparer<T>.Default : new ConstructedEqualityComparer(BuildEquals(), BuildGetHashCode());
    }
}