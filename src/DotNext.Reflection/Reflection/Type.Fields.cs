using System.Reflection;

namespace DotNext.Reflection
{
    public static partial class Type<T>
    {
        /// <summary>
        /// Provides typed access to instance field declared in type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="V">Type of field value.</typeparam>
        public static class Field<V>
        {
            private sealed class InstanceFields : MemberCache<FieldInfo, Field<T, V>>
            {
                internal static readonly InstanceFields Public = new InstanceFields(false);
                internal static readonly InstanceFields NonPublic = new InstanceFields(true);

                private readonly bool nonPublic;
                private InstanceFields(bool nonPublic) => this.nonPublic = nonPublic;

                private protected override Field<T, V> Create(string fieldName)
                    => Field<T, V>.Reflect(fieldName, nonPublic);
            }

            private sealed class StaticFields : MemberCache<FieldInfo, Reflection.Field<V>>
            {
                internal static readonly StaticFields Public = new StaticFields(false);
                internal static readonly StaticFields NonPublic = new StaticFields(true);

                private readonly bool nonPublic;
                private StaticFields(bool nonPublic) => this.nonPublic = nonPublic;

                private protected override Reflection.Field<V> Create(string fieldName)
                    => Reflection.Field<V>.Reflect<T>(fieldName, nonPublic);
            }

            /// <summary>
            /// Gets instance field.
            /// </summary>
            /// <param name="fieldName">Name of field.</param>
            /// <param name="nonPublic">True to reflect non-public field.</param>
            /// <returns>Instance field; or null, if field doesn't exist.</returns>
            public static Field<T, V> Get(string fieldName, bool nonPublic = false)
                => (nonPublic ? InstanceFields.NonPublic : InstanceFields.Public).GetOrCreate(fieldName);

            /// <summary>
            /// Gets instance field.
            /// </summary>
            /// <param name="fieldName">Name of field.</param>
            /// <param name="nonPublic">True to reflect non-public field.</param>
            /// <returns>Instance field.</returns>
            /// <exception cref="MissingEventException">Field doesn't exist.</exception>
            public static Field<T, V> Require(string fieldName, bool nonPublic = false)
                => Get(fieldName, nonPublic) ?? throw MissingFieldException.Create<T, V>(fieldName);

            /// <summary>
            /// Gets static field.
            /// </summary>
            /// <param name="fieldName">Name of field.</param>
            /// <param name="nonPublic">True to reflect non-public field.</param>
            /// <returns>Instance field; or null, if field doesn't exist.</returns>
            public static Reflection.Field<V> GetStatic(string fieldName, bool nonPublic = false)
                => (nonPublic ? StaticFields.NonPublic : StaticFields.Public).GetOrCreate(fieldName);

            /// <summary>
            /// Gets static field.
            /// </summary>
            /// <param name="fieldName">Name of field.</param>
            /// <param name="nonPublic">True to reflect non-public field.</param>
            /// <returns>Instance field.</returns>
            /// <exception cref="MissingEventException">Field doesn't exist.</exception>
            public static Reflection.Field<V> RequireStatic(string fieldName, bool nonPublic = false)
                => GetStatic(fieldName, nonPublic) ?? throw MissingFieldException.Create<T, V>(fieldName);
        }
    }
}