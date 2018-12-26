using System;
using System.Reflection;
using static System.Linq.Expressions.Expression;

namespace MissingPieces.Reflection
{
    public static partial class Type<T>
    {
        /// <summary>
        /// Provides typed access to instance field declared in type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="F">Type of field value.</typeparam>
        public sealed class InstanceField<F> : Reflection.Field<F>, IField<T, F>
        {
            private sealed class PublicCache : MemberCache<FieldInfo, InstanceField<F>>
            {
                private protected override InstanceField<F> Create(string eventName)
                {
                    var field = RuntimeType.GetField(eventName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
                    return field is null || field.FieldType != typeof(F) ?
                        null :
                        new InstanceField<F>(field);
                }
            }

            private sealed class NonPublicCache : MemberCache<FieldInfo, InstanceField<F>>
            {
                private protected override InstanceField<F> Create(string eventName)
                {
                    var field = RuntimeType.GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    return field is null || field.FieldType != typeof(F) ?
                        null :
                        new InstanceField<F>(field);
                }
            }

            private static readonly MemberCache<FieldInfo, InstanceField<F>> Public = new PublicCache();
            private static readonly MemberCache<FieldInfo, InstanceField<F>> NonPublic = new NonPublicCache();

            private readonly MemberAccess<T, F> accessor;

            private InstanceField(FieldInfo field)
                : base(field)
            {
                var instanceParam = Parameter(field.DeclaringType.MakeArrayType());
                var valueParam = Parameter(field.FieldType.MakeByRefType());
                var actionParam = Parameter(typeof(MemberAction));
                if (field.Attributes.HasFlag(FieldAttributes.InitOnly))
                    accessor = Lambda<MemberAccess<T, F>>(MemberAccess.GetOrSetValue(actionParam, Assign(valueParam, Field(instanceParam, field)), null),
                        instanceParam,
                        valueParam,
                        actionParam).Compile();
                else
                    accessor = Lambda<MemberAccess<T, F>>(MemberAccess.GetOrSetValue(actionParam, Assign(valueParam, Field(instanceParam, field)), Assign(Field(instanceParam, field), valueParam)),
                        instanceParam,
                        valueParam,
                        actionParam).Compile();
            }

            public static implicit operator MemberAccess<T, F>(InstanceField<F> field) => field?.accessor;

            public F this[in T instance]
            {
                get => accessor.GetValue(in instance);
                set => accessor.SetValue(in instance, value);
            }

            /// <summary>
            /// Gets instane field.
            /// </summary>
            /// <param name="fieldName">Name of field.</param>
            /// <param name="nonPublic">True to reflect non-public field.</param>
            /// <returns>Instance field; or null, if field doesn't exist.</returns>
            public static InstanceField<F> Get(string fieldName, bool nonPublic = false)
                => (nonPublic ? NonPublic : Public).GetOrCreate(fieldName);

            /// <summary>
            /// Gets instance field.
            /// </summary>
            /// <param name="fieldName">Name of field.</param>
            /// <param name="nonPublic">True to reflect non-public field.</param>
            /// <typeparam name="E">Type of exception to throw if field doesn't exist.</typeparam>
            /// <returns>Instance field.</returns>
            public static InstanceField<F> GetOrThrow<E>(string fieldName, bool nonPublic = false)
                where E : Exception, new()
                => Get(fieldName, nonPublic) ?? throw new E();

            /// <summary>
            /// Gets instance field.
            /// </summary>
            /// <param name="fieldName">Name of field.</param>
            /// <param name="exceptionFactory">A factory used to produce exception.</param>
            /// <param name="nonPublic">True to reflect non-public field.</param>
            /// <typeparam name="E">Type of exception to throw if field doesn't exist.</typeparam>
            /// <returns>Instance field.</returns>
            public static InstanceField<F> GetOrThrow<E>(string fieldName, Func<string, E> exceptionFactory, bool nonPublic = false)
                where E : Exception
                => Get(fieldName, nonPublic) ?? throw exceptionFactory(fieldName);

            /// <summary>
            /// Gets instance field.
            /// </summary>
            /// <param name="fieldName">Name of field.</param>
            /// <param name="nonPublic">True to reflect non-public field.</param>
            /// <returns>Instance field.</returns>
            /// <exception cref="MissingEventException">Field doesn't exist.</exception>
            public static InstanceField<F> GetOrThrow(string fieldName, bool nonPublic = false)
                => GetOrThrow(fieldName, MissingFieldException.Create<T, F>, nonPublic);
        }

        /// <summary>
        /// Provides typed access to static field declared in type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="F">Type of field value.</typeparam>
        public sealed class StaticField<F> : Reflection.Field<F>, IField<F>
        {
            private sealed class PublicCache : MemberCache<FieldInfo, StaticField<F>>
            {
                private protected override StaticField<F> Create(string eventName)
                {
                    var field = RuntimeType.GetField(eventName, BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
                    return field is null || field.FieldType != typeof(F) ?
                        null :
                        new StaticField<F>(field);
                }
            }

            private sealed class NonPublicCache : MemberCache<FieldInfo, StaticField<F>>
            {
                private protected override StaticField<F> Create(string eventName)
                {
                    var field = RuntimeType.GetField(eventName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    return field is null || field.FieldType != typeof(F) ?
                        null :
                        new StaticField<F>(field);
                }
            }

            private static readonly MemberCache<FieldInfo, StaticField<F>> Public = new PublicCache();
            private static readonly MemberCache<FieldInfo, StaticField<F>> NonPublic = new NonPublicCache();

            private readonly MemberAccess<F> accessor;

            private StaticField(FieldInfo field)
                : base(field)
            {
                var valueParam = Parameter(field.FieldType.MakeByRefType());
                var actionParam = Parameter(typeof(MemberAction));
                if (field.Attributes.HasFlag(FieldAttributes.InitOnly))
                    accessor = Lambda<MemberAccess<F>>(MemberAccess.GetOrSetValue(actionParam, Assign(valueParam, Field(null, field)), null),
                        valueParam,
                        actionParam).Compile();
                else
                    accessor = Lambda<MemberAccess<F>>(MemberAccess.GetOrSetValue(actionParam, Assign(valueParam, Field(null, field)), Assign(Field(null, field), valueParam)),
                        valueParam,
                        actionParam).Compile();
            }

            /// <summary>
            /// Gets or sets field value.
            /// </summary>
            public F Value
            {
                get => accessor.GetValue();
                set => accessor.SetValue(value);
            }

            public static implicit operator MemberAccess<F>(StaticField<F> field) => field?.accessor;

            /// <summary>
            /// Gets static field.
            /// </summary>
            /// <param name="fieldName">Name of field.</param>
            /// <param name="nonPublic">True to reflect non-public field.</param>
            /// <returns>Static field; or null, if field doesn't exist.</returns>
            public static StaticField<F> Get(string fieldName, bool nonPublic = false)
                => (nonPublic ? NonPublic : Public).GetOrCreate(fieldName);

            /// <summary>
            /// Gets static field.
            /// </summary>
            /// <param name="fieldName">Name of field.</param>
            /// <param name="nonPublic">True to reflect non-public field.</param>
            /// <typeparam name="E">Type of exception to throw if field doesn't exist.</typeparam>
            /// <returns>Static field.</returns>
            public static StaticField<F> GetOrThrow<E>(string fieldName, bool nonPublic = false)
                where E : Exception, new()
                => Get(fieldName, nonPublic) ?? throw new E();

            /// <summary>
            /// Gets static field.
            /// </summary>
            /// <param name="fieldName">Name of field.</param>
            /// <param name="exceptionFactory">A factory used to produce exception.</param>
            /// <param name="nonPublic">True to reflect non-public field.</param>
            /// <typeparam name="E">Type of exception to throw if field doesn't exist.</typeparam>
            /// <returns>Static field.</returns>
            public static StaticField<F> GetOrThrow<E>(string fieldName, Func<string, E> exceptionFactory, bool nonPublic = false)
                where E : Exception
                => Get(fieldName, nonPublic) ?? throw exceptionFactory(fieldName);

            /// <summary>
            /// Gets static field.
            /// </summary>
            /// <param name="fieldName">Name of field.</param>
            /// <param name="nonPublic">True to reflect non-public field.</param>
            /// <returns>Static field.</returns>
            /// <exception cref="MissingEventException">Field doesn't exist.</exception>
            public static StaticField<F> GetOrThrow(string fieldName, bool nonPublic = false)
                => GetOrThrow(fieldName, MissingFieldException.Create<T, F>, nonPublic);
        }
    }
}