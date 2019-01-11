using System;
using System.Reflection;
using System.Collections.Generic;
using static System.Linq.Expressions.Expression;

namespace Cheats.Reflection
{
    /// <summary>
    /// Provides typed access to class or value type metadata.
    /// </summary>
    /// <typeparam name="T">Reflected type.</typeparam>
    public static partial class Type<T>
    {
        /// <summary>
        /// Gets reflected type.
        /// </summary>
        public static Type RuntimeType => typeof(T);

        /// <summary>
        /// Returns default value for this type.
        /// </summary>
        public static T Default => default;

        private static readonly System.Linq.Expressions.DefaultExpression DefaultExpression = Default(RuntimeType);

        /// <summary>
        /// Checks whether the specified value is default value.
        /// </summary>
        public static readonly Predicate<T> IsDefault;

        /// <summary>
        /// Provides smart hash code computation.
        /// </summary>
        /// <remarks>
        /// For reference types, this delegate always calls <see cref="object.GetHashCode"/> virtual method.
        /// For value type, it calls <see cref="object.GetHashCode"/> if it is overridden by the value type; otherwise,
        /// it calls <see cref="ValueTypeCheats.BitwiseHashCode{T}(T)"/>.
        /// </remarks>
        public new static readonly Operator<T, int> GetHashCode;

        /// <summary>
        /// Provides smart equality check.
        /// </summary>
        /// <remarks>
        /// If type <typeparamref name="T"/> has equality operator then use it.
        /// Otherwise, for reference types, this delegate always calls <see cref="object.Equals(object, object)"/> method.
        /// For value type, it calls equality operator or <see cref="IEquatable{T}.Equals(T)"/> if it is implemented by the value type; else,
        /// it calls <see cref="ValueTypeCheats.BitwiseEquals{T}(T, T)"/>.
        /// </remarks>
        public new static readonly Operator<T, T, bool> Equals;

        static Type()
        {
            var inputParam = Parameter(RuntimeType.MakeByRefType(), "obj");
            var secondParam = Parameter(RuntimeType.MakeByRefType(), "other");
            //1. try to resolve equality operator
            Equals = Operator<T>.Get<bool>(BinaryOperator.Equal, OperatorLookup.Overloaded);
            if(RuntimeType.IsValueType)
            {
                //default checker
                var method = typeof(ValueType<int>).GetGenericTypeDefinition().MakeGenericType(RuntimeType).GetMethod(nameof(ValueType<int>.IsDefault));
                IsDefault = method.CreateDelegate<Predicate<T>>();
                //hash code calculator
                method = RuntimeType.GetHashCodeMethod();
                if(method is null)
                {
                    method = typeof(ValueTypeCheats)
                                .GetMethod(nameof(ValueTypeCheats.BitwiseHashCode), BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
                                .MakeGenericMethod(RuntimeType);
                    GetHashCode = Lambda<Operator<T, int>>(Call(null, method, inputParam), inputParam).Compile();
                }
                else
                    GetHashCode = method.CreateDelegate<Operator<T, int>>();
                //equality checker
                if(Equals is null)
                    //2. try to find IEquatable.Equals implementation
                    if(typeof(IEquatable<T>).IsAssignableFrom(RuntimeType))
                    {
                        method = typeof(IEquatable<T>).GetMethod(nameof(IEquatable<T>.Equals), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        Equals = Lambda<Operator<T, T, bool>>(Call(inputParam, method, secondParam), inputParam, secondParam).Compile();
                    }
                    //3. Use bitwise equality
                    else
                    {
                        method = typeof(ValueTypeCheats).GetMethod(nameof(ValueTypeCheats.BitwiseEquals), BindingFlags.Static | BindingFlags.DeclaredOnly | BindingFlags.Public)
                            .MakeGenericMethod(RuntimeType);
                        Equals = Lambda<Operator<T, T, bool>>(Call(null, method, inputParam, secondParam), inputParam, secondParam).Compile();
                    }
            }
            else
            {
                //default checker
                IsDefault = new Predicate<object>(input => input is null).ConvertDelegate<Predicate<T>>();
                //hash code calculator
                GetHashCode = Lambda<Operator<T, int>>(Call(inputParam, typeof(object).GetHashCodeMethod()), inputParam).Compile();
                //equality checker
                if(Equals is null)
                    Equals = Lambda<Operator<T, T, bool>>(Call(null, typeof(object).GetMethod(nameof(object.Equals), BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly), inputParam, secondParam), inputParam, secondParam).Compile();
            }
        }

        /// <summary>
        /// Determines whether an instance of a specified type can be assigned to an instance of the current type.
        /// </summary>
        /// <typeparam name="U">The type to compare with the current type.</typeparam>
        /// <returns>True, if instance of type <typeparamref name="U"/> can be assigned to type <typeparamref name="T"/>.</returns>
        public static bool IsAssignableFrom<U>() => RuntimeType.IsAssignableFrom(typeof(U));

        public static bool IsAssignableTo<U>() => Type<U>.IsAssignableFrom<T>();

        public static Optional<T> TryConvert<U>(U value)
        {
            Operator<U, T> converter = Type<U>.Operator.Get<T>(UnaryOperator.Convert);
            return converter is null ? Optional<T>.Empty : converter(value);
        }

        public static bool TryConvert<U>(U value, out T result) => TryConvert(value).TryGet(out result);

        /// <summary>
        /// Converts object into type <typeparamref name="T"/>.
        /// </summary>
        /// <remarks>
        /// Semantics of this method includes typecast as well as conversion between numeric types
        /// and implicit/explicit cast operators.
        /// </remarks>
        /// <param name="value">The value to convert.</param>
        /// <typeparam name="U">Type of value to convert.</typeparam>
        /// <returns>Converted value.</returns>
        /// <exception cref="InvalidCastException">Cannot convert values.</exception>
        public static T Convert<U>(U value) => TryConvert<U>(value).OrThrow<InvalidCastException>();
    }
}