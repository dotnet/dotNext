using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using static System.Linq.Expressions.Expression;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace DotNext.Reflection
{
    using Intrinsics = Runtime.Intrinsics;

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
        public static T? Default => Intrinsics.DefaultOf<T>();

        /// <summary>
        /// Provides smart hash code computation.
        /// </summary>
        /// <remarks>
        /// For reference types, this delegate always calls <see cref="object.GetHashCode"/> virtual method.
        /// For value type, it calls <see cref="object.GetHashCode"/> if it is overridden by the value type; otherwise,
        /// it calls <see cref="BitwiseComparer{T}.GetHashCode(in T, bool)"/>.
        /// </remarks>
        public static new readonly Operator<T, int> GetHashCode;

        /// <summary>
        /// Provides smart equality check.
        /// </summary>
        /// <remarks>
        /// If type <typeparamref name="T"/> has equality operator then use it.
        /// Otherwise, for reference types, this delegate always calls <see cref="object.Equals(object, object)"/> method.
        /// For value type, it calls equality operator or <see cref="IEquatable{T}.Equals(T)"/> if it is implemented by the value type; else,
        /// it calls <see cref="BitwiseComparer{T}.Equals{G}"/>.
        /// </remarks>
        public static new readonly Operator<T, T, bool> Equals;

        static Type()
        {
            var inputParam = Parameter(RuntimeType.MakeByRefType(), "obj");
            var secondParam = Parameter(RuntimeType.MakeByRefType(), "other");

            // 1. try to resolve equality operator
            Operator<T, T, bool>? equalsOp = Operator<T>.Get<bool>(BinaryOperator.Equal, OperatorLookup.Overloaded);
            if (RuntimeType.IsValueType)
            {
                // hash code calculator
                var method = RuntimeType.GetHashCodeMethod();
                if (method is null)
                {
                    method = typeof(BitwiseComparer<>)
                                .MakeGenericType(RuntimeType)
                                .GetMethod(nameof(BitwiseComparer<int>.GetHashCode), new[] { RuntimeType.MakeByRefType(), typeof(bool) });
                    Debug.Assert(method is not null);
                    GetHashCode = Lambda<Operator<T, int>>(Call(null, method, inputParam, Constant(true)), inputParam).Compile();
                }
                else
                {
                    GetHashCode = method.CreateDelegate<Operator<T, int>>();
                }

                // equality checker
                if (equalsOp is null)
                {
                    // 2. try to find IEquatable.Equals implementation
                    if (typeof(IEquatable<T>).IsAssignableFrom(RuntimeType))
                    {
                        method = typeof(IEquatable<T>).GetMethod(nameof(IEquatable<T>.Equals), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        Debug.Assert(method is not null);
                        equalsOp = Lambda<Operator<T, T, bool>>(Call(inputParam, method, secondParam), inputParam, secondParam).Compile();
                    }

                    // 3. Use bitwise equality
                    else
                    {
                        method = typeof(BitwiseComparer<>)
                            .MakeGenericType(RuntimeType)
                            .GetMethod(nameof(BitwiseComparer<int>.Equals), BindingFlags.Static | BindingFlags.DeclaredOnly | BindingFlags.Public)
                            ?.MakeGenericMethod(RuntimeType);
                        Debug.Assert(method is not null);
                        equalsOp = Lambda<Operator<T, T, bool>>(Call(null, method, inputParam, secondParam), inputParam, secondParam).Compile();
                    }
                }
            }
            else
            {
                // hash code calculator
                GetHashCode = Lambda<Operator<T, int>>(Call(inputParam, typeof(object).GetHashCodeMethod()!), inputParam).Compile();

                // equality checker
                if (equalsOp is null)
                {
                    var equalsMethod = typeof(object).GetMethod(nameof(Equals), BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly);
                    Debug.Assert(equalsMethod is not null);
                    equalsOp = Lambda<Operator<T, T, bool>>(Call(null, equalsMethod, inputParam, secondParam), inputParam, secondParam).Compile();
                }
            }

            Equals = equalsOp;
        }

        /// <summary>
        /// Calls static constructor of type <typeparamref name="T"/>.
        /// </summary>
        /// <remarks>
        /// This method doesn't call static constructor if type is already initialized.
        /// </remarks>
        public static void Initialize() => RunClassConstructor(Intrinsics.TypeOf<T>());

        /// <summary>
        /// Determines whether an instance of a specified type can be assigned to an instance of the current type.
        /// </summary>
        /// <typeparam name="TOther">The type to compare with the current type.</typeparam>
        /// <returns><see langword="true"/>, if instance of type <typeparamref name="TOther"/> can be assigned to type <typeparamref name="T"/>.</returns>
        public static bool IsAssignableFrom<TOther>() => RuntimeType.IsAssignableFrom(typeof(TOther));

        /// <summary>
        /// Determines whether an instance of the current type can be assigned to an instance of the specified type.
        /// </summary>
        /// <typeparam name="TOther">The type to compare with the current type.</typeparam>
        /// <returns><see langword="true"/>, if instance of type <typeparamref name="T"/> can be assigned to type <typeparamref name="TOther"/>.</returns>
        public static bool IsAssignableTo<TOther>() => Type<TOther>.IsAssignableFrom<T>();

        /// <summary>
        /// Applies type cast to the given object respecting overloaded cast operator.
        /// </summary>
        /// <typeparam name="TSource">The type of object to be converted.</typeparam>
        /// <param name="value">The value to be converted.</param>
        /// <returns>Optional conversion result if it is supported for the given type.</returns>
        public static Optional<T> TryConvert<TSource>(TSource value)
        {
            Operator<TSource, T>? converter = Type<TSource>.Operator.Get<T>(UnaryOperator.Convert);
            return converter is null ? Optional<T>.None : converter(value)!;
        }

        /// <summary>
        /// Applies type cast to the given object respecting overloaded cast operator.
        /// </summary>
        /// <typeparam name="TSource">The type of object to be converted.</typeparam>
        /// <param name="value">The value to be converted.</param>
        /// <param name="result">The conversion result.</param>
        /// <returns><see langword="true"/>, if conversion is supported by the given type; otherwise, <see langword="false"/>.</returns>
        public static bool TryConvert<TSource>(TSource value, [MaybeNullWhen(false)]out T result) => TryConvert(value).TryGet(out result);

        /// <summary>
        /// Converts object into type <typeparamref name="T"/>.
        /// </summary>
        /// <remarks>
        /// Semantics of this method includes typecast as well as conversion between numeric types
        /// and implicit/explicit cast operators.
        /// </remarks>
        /// <param name="value">The value to convert.</param>
        /// <typeparam name="TSource">Type of value to convert.</typeparam>
        /// <returns>Converted value.</returns>
        /// <exception cref="InvalidCastException">Cannot convert values.</exception>
        [return: NotNull]
        public static T Convert<TSource>(TSource value) => TryConvert(value).OrThrow<InvalidCastException>();
    }
}