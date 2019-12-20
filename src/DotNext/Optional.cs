using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace DotNext
{
    using static Reflection.TypeExtensions;

    /// <summary>
    /// Various extension and factory methods for constructing optional value.
    /// </summary>
    public static class Optional
    {
        /// <summary>
        /// If a value is present, returns the value, otherwise <see langword="null"/>.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="task">The task returning optional value.</param>
        /// <returns>Nullable value.</returns>
        public static async Task<T?> OrNull<T>(this Task<Optional<T>> task) where T : struct
            => (await task.ConfigureAwait(false)).OrNull();

        /// <summary>
        /// Returns the value if present; otherwise return default value.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="task">The task returning optional value.</param>
        /// <param name="defaultValue">The value to be returned if there is no value present.</param>
        /// <returns>The value, if present, otherwise default</returns>
        public static async Task<T> Or<T>(this Task<Optional<T>> task, T defaultValue)
            => (await task.ConfigureAwait(false)).Or(defaultValue);

        /// <summary>
		/// If a value is present, apply the provided mapping function to it, and if the result is 
		/// non-null, return an Optional describing the result. Otherwise returns <see cref="Optional{T}.Empty"/>.
		/// </summary>
        /// <typeparam name="I">The type of stored in the Optional container.</typeparam>
		/// <typeparam name="O">The type of the result of the mapping function.</typeparam>
        /// <param name="task">The task containing Optional value.</param>
		/// <param name="converter">A mapping function to be applied to the value, if present.</param>
		/// <returns>An Optional describing the result of applying a mapping function to the value of this Optional, if a value is present, otherwise <see cref="Optional{T}.Empty"/>.</returns>
		public static async Task<Optional<O>> Convert<I, O>(this Task<Optional<I>> task, Converter<I, O> converter)
            => (await task.ConfigureAwait(false)).Convert(converter);

        /// <summary>
        /// If a value is present, returns the value, otherwise throw exception.
        /// </summary>
        /// <param name="task">The task returning optional value.</param>
        /// <typeparam name="T">Type of the value.</typeparam>
        /// <typeparam name="E">Type of exception to throw.</typeparam>
        /// <returns>The value, if present.</returns>
        public static async Task<T> OrThrow<T, E>(this Task<Optional<T>> task)
            where E : Exception, new()
            => (await task.ConfigureAwait(false)).OrThrow<E>();

        /// <summary>
        /// If a value is present, returns the value, otherwise throw exception.
        /// </summary>
        /// <typeparam name="T">Type of the value.</typeparam>
        /// <typeparam name="E">Type of exception to throw.</typeparam>
        /// <param name="task">The task returning optional value.</param>
        /// <param name="exceptionFactory">Exception factory.</param>
        /// <returns>The value, if present.</returns>
        public static async Task<T> OrThrow<T, E>(this Task<Optional<T>> task, Func<E> exceptionFactory)
            where E : Exception
            => (await task.ConfigureAwait(false)).OrThrow(exceptionFactory);

        /// <summary>
        /// Returns the value if present; otherwise invoke delegate.
        /// </summary>
        /// <typeparam name="T">Type of the value.</typeparam>
        /// <param name="task">The task returning optional value.</param>
        /// <param name="defaultFunc">A delegate to be invoked if value is not present.</param>
        /// <returns>The value, if present, otherwise returned from delegate.</returns>
        public static async Task<T> OrInvoke<T>(this Task<Optional<T>> task, Func<T> defaultFunc)
            => (await task.ConfigureAwait(false)).OrInvoke(defaultFunc);

        /// <summary>
        /// If a value is present, returns the value, otherwise return default value.
        /// </summary>
        /// <typeparam name="T">Type of the value.</typeparam>
        /// <param name="task">The task returning optional value.</param>
        /// <returns>The value, if present, otherwise default</returns>
        public static async Task<T> OrDefault<T>(this Task<Optional<T>> task)
            => (await task.ConfigureAwait(false)).OrDefault();

        /// <summary>
        /// If a value is present, and the value matches the given predicate, 
        /// return an Optional describing the value, otherwise return an empty Optional.
        /// </summary>
        /// <typeparam name="T">Type of the value.</typeparam>
        /// <param name="task">The task returning optional value.</param>
        /// <param name="condition">A predicate to apply to the value, if present.</param>
        /// <returns>An Optional describing the value of this Optional if a value is present and the value matches the given predicate, otherwise an empty Optional</returns>
        public static async Task<Optional<T>> If<T>(this Task<Optional<T>> task, Predicate<T> condition)
            => (await task.ConfigureAwait(false)).If(condition);

        /// <summary>
        /// Indicates that specified type is optional type.
        /// </summary>
        /// <returns><see langword="true"/>, if specified type is optional type; otherwise, <see langword="false"/>.</returns>
        public static bool IsOptional(this Type optionalType) => optionalType.IsGenericInstanceOf(typeof(Optional<>));

        /// <summary>
        /// Returns the underlying type argument of the specified optional type.
        /// </summary>
        /// <param name="optionalType">Optional type.</param>
        /// <returns>Underlying type argument of optional type; otherwise, <see langword="null"/>.</returns>
        public static Type GetUnderlyingType(Type optionalType) => IsOptional(optionalType) ? optionalType.GetGenericArguments()[0] : null;

        /// <summary>
        /// Constructs optional value from nullable reference type.
        /// </summary>
        /// <typeparam name="T">Type of value.</typeparam>
        /// <param name="value">The value to convert.</param>
        /// <returns>The value wrapped into Optional container.</returns>
        public static Optional<T> ToOptional<T>(this in T? value)
            where T : struct
            => value ?? Optional<T>.Empty;

        /// <summary>
        /// If a value is present, returns the value, otherwise <see langword="null"/>.
        /// </summary>
        /// <typeparam name="T">Value type.</typeparam>
        /// <param name="value">Optional value.</param>
        /// <returns>Nullable value.</returns>
        public static T? OrNull<T>(this in Optional<T> value)
            where T : struct
            => value.TryGet(out var result) ? new T?(result) : null;

        /// <summary>
        /// Returns the second value if the first is empty.
        /// </summary>
        /// <param name="first">The first optional value.</param>
        /// <param name="second">The second optional value.</param>
        /// <typeparam name="T">Type of value.</typeparam>
        /// <returns>The second value if the first is empty; otherwise, the first value.</returns>
        public static ref readonly Optional<T> Coalesce<T>(this in Optional<T> first, in Optional<T> second) => ref first.IsPresent ? ref first : ref second;
    }

    /// <summary>
    /// A container object which may or may not contain a value.
    /// </summary>
    /// <typeparam name="T">Type of value.</typeparam>
    [Serializable]
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Optional<T> : IEquatable<Optional<T>>, IEquatable<T>, IStructuralEquatable, ISerializable
    {
        private const string IsPresentSerData = "IsPresent";
        private const string ValueSerData = "Value";
        private const byte ReferenceType = 0;
        private const byte ValueType = 1;
        private const byte NullableType = 2;

        private static readonly byte type;

        static Optional()
        {
            var targetType = typeof(T);
            if (targetType.IsOneOf(typeof(void), typeof(ValueTuple), typeof(DBNull)))
                type = byte.MaxValue;
            else if (targetType.IsValueType)
                type = targetType.IsGenericInstanceOf(typeof(Nullable<>)) || targetType.IsOptional() ? NullableType : ValueType;
            else
                type = ReferenceType;
        }

        private readonly T value;

        /// <summary>
        /// Constructs non-empty container.
        /// </summary>
        /// <param name="value">A value to be placed into container.</param>
        public Optional(T value)
        {
            this.value = value;
            switch (type)
            {
                default:
                    IsPresent = false;
                    break;
                case ReferenceType:
                    IsPresent = value != null;
                    break;
                case ValueType:
                    IsPresent = true;
                    break;
                case NullableType:
                    IsPresent = !value.Equals(null);
                    break;
            }
        }

        [SuppressMessage("Usage", "CA1801", Justification = "context is required by .NET serialization framework")]
        private Optional(SerializationInfo info, StreamingContext context)
        {
            value = (T)info.GetValue(ValueSerData, typeof(T));
            IsPresent = info.GetBoolean(IsPresentSerData);
        }

        /// <summary>
        /// Represents optional container without value.
        /// </summary>
        public static Optional<T> Empty => default;

        /// <summary>
        /// Indicates whether the value is present.
        /// </summary>
        public bool IsPresent { get; }

        /// <summary>
        /// Attempts to extract value from container if it is present.
        /// </summary>
        /// <param name="value">Extracted value.</param>
        /// <returns><see langword="true"/> if value is present; otherwise, <see langword="false"/>.</returns>
        public bool TryGet(out T value)
        {
            value = this.value;
            return IsPresent;
        }

        /// <summary>
        /// Returns the value if present; otherwise return default value.
        /// </summary>
        /// <param name="defaultValue">The value to be returned if there is no value present.</param>
        /// <returns>The value, if present, otherwise <paramref name="defaultValue"/>.</returns>
        public T Or(T defaultValue) => IsPresent ? value : defaultValue;

        /// <summary>
        /// If a value is present, returns the value, otherwise throw exception.
        /// </summary>
        /// <typeparam name="E">Type of exception to throw.</typeparam>
        /// <returns>The value, if present.</returns>
        public T OrThrow<E>()
            where E : Exception, new()
            => IsPresent ? value : throw new E();

        /// <summary>
        /// If a value is present, returns the value, otherwise throw exception.
        /// </summary>
        /// <typeparam name="E"></typeparam>
        /// <param name="exceptionFactory">Exception factory.</param>
        /// <returns>The value, if present.</returns>
        public T OrThrow<E>(in ValueFunc<E> exceptionFactory)
            where E : Exception
            => IsPresent ? value : throw exceptionFactory.Invoke();

        /// <summary>
        /// If a value is present, returns the value, otherwise throw exception.
        /// </summary>
        /// <typeparam name="E"></typeparam>
        /// <param name="exceptionFactory">Exception factory.</param>
        /// <returns>The value, if present.</returns>
        public T OrThrow<E>(Func<E> exceptionFactory)
            where E : Exception
            => OrThrow(new ValueFunc<E>(exceptionFactory, true));

        /// <summary>
        /// Returns the value if present; otherwise invoke delegate.
        /// </summary>
        /// <param name="defaultFunc">A delegate to be invoked if value is not present.</param>
        /// <returns>The value, if present, otherwise returned from delegate.</returns>
        public T OrInvoke(in ValueFunc<T> defaultFunc) => IsPresent ? value : defaultFunc.Invoke();

        /// <summary>
        /// Returns the value if present; otherwise invoke delegate.
        /// </summary>
        /// <param name="defaultFunc">A delegate to be invoked if value is not present.</param>
        /// <returns>The value, if present, otherwise returned from delegate.</returns>
        public T OrInvoke(Func<T> defaultFunc) => OrInvoke(new ValueFunc<T>(defaultFunc, true));

        /// <summary>
        /// If a value is present, returns the value, otherwise return default value.
        /// </summary>
        /// <returns>The value, if present, otherwise default</returns>
        public T OrDefault() => value;

        /// <summary>
        /// If a value is present, returns the value, otherwise throw exception.
        /// </summary>
        /// <exception cref="InvalidOperationException">No value is present.</exception>
        public T Value => IsPresent ? value : throw new InvalidOperationException(ExceptionMessages.OptionalNoValue);

        /// <summary>
        /// If a value is present, apply the provided mapping function to it, and if the result is 
        /// non-null, return an Optional describing the result. Otherwise returns <see cref="Empty"/>.
        /// </summary>
        /// <typeparam name="U">The type of the result of the mapping function.</typeparam>
        /// <param name="mapper">A mapping function to be applied to the value, if present.</param>
        /// <returns>An Optional describing the result of applying a mapping function to the value of this Optional, if a value is present, otherwise <see cref="Empty"/>.</returns>
        public Optional<U> Convert<U>(in ValueFunc<T, U> mapper) => IsPresent ? mapper.Invoke(value) : Optional<U>.Empty;

        /// <summary>
        /// If a value is present, apply the provided mapping function to it, and if the result is 
        /// non-null, return an Optional describing the result. Otherwise returns <see cref="Empty"/>.
        /// </summary>
        /// <typeparam name="U">The type of the result of the mapping function.</typeparam>
        /// <param name="mapper">A mapping function to be applied to the value, if present.</param>
        /// <returns>An Optional describing the result of applying a mapping function to the value of this Optional, if a value is present, otherwise <see cref="Empty"/>.</returns>
        public Optional<U> Convert<U>(Converter<T, U> mapper) => Convert(mapper.AsValueFunc(true));

        /// <summary>
        /// If a value is present, apply the provided mapping function to it, and if the result is 
		/// non-null, return an Optional describing the result. Otherwise returns <see cref="Empty"/>.
        /// </summary>
        /// <typeparam name="U">The type of the result of the mapping function.</typeparam>
        /// <param name="mapper">A mapping function to be applied to the value, if present.</param>
        /// <returns>An Optional describing the result of applying a mapping function to the value of this Optional, if a value is present, otherwise <see cref="Empty"/>.</returns>
		public Optional<U> Convert<U>(in ValueFunc<T, Optional<U>> mapper) => IsPresent ? mapper.Invoke(value) : Optional<U>.Empty;

        /// <summary>
        /// If a value is present, apply the provided mapping function to it, and if the result is 
		/// non-null, return an Optional describing the result. Otherwise returns <see cref="Empty"/>.
        /// </summary>
        /// <typeparam name="U">The type of the result of the mapping function.</typeparam>
        /// <param name="mapper">A mapping function to be applied to the value, if present.</param>
        /// <returns>An Optional describing the result of applying a mapping function to the value of this Optional, if a value is present, otherwise <see cref="Empty"/>.</returns>
		public Optional<U> Convert<U>(Converter<T, Optional<U>> mapper) => Convert(mapper.AsValueFunc(true));

        /// <summary>
        /// If a value is present, and the value matches the given predicate, 
        /// return an Optional describing the value, otherwise return an empty Optional.
        /// </summary>
        /// <param name="condition">A predicate to apply to the value, if present.</param>
        /// <returns>An Optional describing the value of this Optional if a value is present and the value matches the given predicate, otherwise an empty Optional</returns>
        public Optional<T> If(in ValueFunc<T, bool> condition) => IsPresent && condition.Invoke(value) ? this : Empty;

        /// <summary>
        /// If a value is present, and the value matches the given predicate, 
        /// return an Optional describing the value, otherwise return an empty Optional.
        /// </summary>
        /// <param name="condition">A predicate to apply to the value, if present.</param>
        /// <returns>An Optional describing the value of this Optional if a value is present and the value matches the given predicate, otherwise an empty Optional</returns>
        public Optional<T> If(Predicate<T> condition) => If(condition.AsValueFunc(true));

        /// <summary>
        /// Returns textual representation of this object.
        /// </summary>
        /// <returns>The textual representation of this object.</returns>
		public override string ToString() => IsPresent ? value.ToString() : "<EMPTY>";

        /// <summary>
        /// Computes hash code of the stored value.
        /// </summary>
        /// <returns>The hash code of the stored value.</returns>
        /// <remarks>
        /// This method calls <see cref="object.GetHashCode()"/>
        /// for the object <see cref="Value"/>.
        /// </remarks>
		public override int GetHashCode() => IsPresent ? value.GetHashCode() : 0;

        /// <summary>
        /// Determines whether this container stored the same
        /// value as the specified value.
        /// </summary>
        /// <param name="other">Other value to compare.</param>
        /// <returns><see langword="true"/> if <see cref="Value"/> is equal to <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
		public bool Equals(T other) => IsPresent && value.Equals(other);

        /// <summary>
        /// Determines whether this container stores
        /// the same value as other.
        /// </summary>
        /// <param name="other">Other container to compare.</param>
        /// <returns><see langword="true"/> if this container stores the same value as <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public bool Equals(Optional<T> other)
        {
            switch (IsPresent.ToInt32() + other.IsPresent.ToInt32())
            {
                default:
                    return true;
                case 1:
                    return false;
                case 2:
                    return value.Equals(other.value);
            }
        }

        /// <summary>
        /// Determines whether this container stores
        /// the same value as other.
        /// </summary>
        /// <param name="other">Other container to compare.</param>
        /// <returns><see langword="true"/> if this container stores the same value as <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other)
        {
            switch (other)
            {
                case null:
                    return IsPresent == false;
                case Optional<T> optional:
                    return Equals(optional);
                case T value:
                    return Equals(value);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Performs equality check between stored value
        /// and the specified value using method <see cref="IEqualityComparer.Equals(object, object)"/>.
        /// </summary>
        /// <param name="other">Other object to compare with <see cref="Value"/>.</param>
        /// <param name="comparer">The comparer implementing custom equality check.</param>
        /// <returns><see langword="true"/> if <paramref name="other"/> is equal to <see cref="Value"/> using custom check; otherwise, <see langword="false"/>.</returns>
		public bool Equals(object other, IEqualityComparer comparer)
            => other is T && IsPresent && comparer.Equals(value, other);

        /// <summary>
        /// Computes hash code for the stored value 
        /// using method <see cref="IEqualityComparer.GetHashCode(object)"/>.
        /// </summary>
        /// <param name="comparer">The comparer implementing hash code function.</param>
        /// <returns>The hash code of <see cref="Value"/>.</returns>
		public int GetHashCode(IEqualityComparer comparer)
            => IsPresent ? comparer.GetHashCode(value) : 0;

        /// <summary>
        /// Wraps value into Optional container.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Optional<T>(T value) => new Optional<T>(value);

        /// <summary>
        /// Extracts value stored in the Optional container.
        /// </summary>
        /// <param name="optional">The container.</param>
        /// <exception cref="InvalidOperationException">No value is present.</exception>
        public static explicit operator T(in Optional<T> optional) => optional.Value;

        /// <summary>
        /// Determines whether two containers store the same value.
        /// </summary>
        /// <param name="first">The first container to compare.</param>
        /// <param name="second">The second container to compare.</param>
        /// <returns><see langword="true"/>, if both containers store the same value; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in Optional<T> first, in Optional<T> second)
        {
            switch (first.IsPresent.ToInt32() + second.IsPresent.ToInt32())
            {
                default:
                    return true;
                case 1:
                    return false;
                case 2:
                    return first.value.Equals(second.value);
            }
        }

        /// <summary>
        /// Determines whether two containers store the different values.
        /// </summary>
        /// <param name="first">The first container to compare.</param>
        /// <param name="second">The second container to compare.</param>
        /// <returns><see langword="true"/>, if both containers store the different values; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in Optional<T> first, in Optional<T> second)
        {
            switch (first.IsPresent.ToInt32() + second.IsPresent.ToInt32())
            {
                default:
                    return false;
                case 1:
                    return true;
                case 2:
                    return !first.value.Equals(second.value);
            }
        }

        /// <summary>
        /// Returns non-empty container.
        /// </summary>
        /// <param name="first">The first container.</param>
        /// <param name="second">The second container.</param>
        /// <returns>The first non-empty container.</returns>
        /// <seealso cref="Optional.Coalesce{T}"/>
		public static Optional<T> operator |(in Optional<T> first, in Optional<T> second)
            => first.IsPresent ? first : second;

        /// <summary>
        /// Determines whether two containers are empty or have values.
        /// </summary>
        /// <param name="first">The first container.</param>
        /// <param name="second">The second container.</param>
        /// <returns><see langword="true"/>, if both containers are empty or have values; otherwise, <see langword="false"/>.</returns>
        public static Optional<T> operator ^(in Optional<T> first, in Optional<T> second)
        {
            switch (first.IsPresent.ToInt32() - second.IsPresent.ToInt32())
            {
                default:
                    return Empty;
                case -1:
                    return second;
                case 1:
                    return first;
            }
        }

        /// <summary>
        /// Checks whether the container has value.
        /// </summary>
        /// <param name="optional">The container to check.</param>
        /// <returns><see langword="true"/> if this container has value; otherwise, <see langword="false"/>.</returns>
        /// <see cref="IsPresent"/>
        public static bool operator true(in Optional<T> optional) => optional.IsPresent;

        /// <summary>
        /// Checks whether the container has no value.
        /// </summary>
        /// <param name="optional">The container to check.</param>
        /// <returns><see langword="true"/> if this container has no value; otherwise, <see langword="false"/>.</returns>
        /// <see cref="IsPresent"/>
        public static bool operator false(in Optional<T> optional) => !optional.IsPresent;

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(IsPresentSerData, IsPresent);
            info.AddValue(ValueSerData, value, typeof(T));
        }
    }
}