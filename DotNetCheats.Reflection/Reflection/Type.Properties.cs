using System;
using System.Reflection;
using static System.Linq.Expressions.Expression;
using System.Runtime.CompilerServices;

namespace Cheats.Reflection
{
    public static partial class Type<T>
    {
        /// <summary>
        /// Provides typed access to static declared in type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="P">Type of property.</typeparam>
        public sealed class StaticProperty<P> : Property<P>, IProperty<P>
        {
            private sealed class Cache : MemberCache<PropertyInfo, StaticProperty<P>>
            {
                private readonly BindingFlags flags;

                internal Cache(BindingFlags flags) => this.flags = flags;

                private protected override StaticProperty<P> Create(string propertyName)
                {
                    var property = RuntimeType.GetProperty(propertyName, flags);
                    return property == null ? null : new StaticProperty<P>(property, flags.HasFlag(BindingFlags.NonPublic));
                }
            }

            private static readonly Cache Public = new Cache(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            private static readonly Cache NonPublic = new Cache(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

            private readonly MemberAccess<P> accessor;
            private StaticProperty(PropertyInfo property, bool nonPublic)
                : base(property)
            {
                var valueParam = Parameter(property.PropertyType.MakeByRefType());
                var actionParam = Parameter(typeof(MemberAction));

                var getter = property.GetGetMethod(nonPublic);
                var setter = property.GetSetMethod(nonPublic);

                if (getter is null) //write-only
                    accessor = Lambda<MemberAccess<P>>(MemberAccess.GetOrSetValue(actionParam, null, Call(null, setter, valueParam)),
                        valueParam,
                        actionParam).Compile();
                else if (setter is null) //read-only
                    accessor = Lambda<MemberAccess<P>>(MemberAccess.GetOrSetValue(actionParam, Assign(valueParam, Call(null, getter)), null),
                        valueParam,
                        actionParam).Compile();
                else //read-write
                    accessor = Lambda<MemberAccess<P>>(MemberAccess.GetOrSetValue(actionParam, Assign(valueParam, Call(null, getter)), Call(null, setter, valueParam)),
                        valueParam,
                        actionParam).Compile();
            }

            // public new Method<MemberAccess.Getter<P>> GetGetMethod(bool nonPublic)
            // {
            //     var getter = base.GetGetMethod(nonPublic);
            //     return getter == null ? null : StaticMethod<MemberAccess.Getter<P>>.Get(getter.Name, nonPublic);
            // }

            // public new Method<MemberAccess.Getter<P>> GetMethod
            // {
            //     get
            //     {
            //         var getter = base.GetMethod;
            //         return getter == null ? null : StaticMethod<MemberAccess.Getter<P>>.Get(getter.Name, !getter.IsPublic);
            //     }
            // }
            // public new Method<MemberAccess.Setter<P>> SetMethod
            // {
            //     get
            //     {
            //         var setter = base.SetMethod;
            //         return setter == null ? null : StaticMethod<MemberAccess.Setter<P>>.Get(setter.Name, !setter.IsPublic);
            //     }
            // }

            // public new Method<MemberAccess.Setter<P>> GetSetMethod(bool nonPublic)
            // {
            //     var setter = base.GetSetMethod(nonPublic);
            //     return setter == null ? null : StaticMethod<MemberAccess.Setter<P>>.Get(setter.Name, nonPublic);
            // }

            /// <summary>
            /// Gets or sets property value.
            /// </summary>
            public P Value
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => accessor.GetValue();
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set => accessor.SetValue(value);
            }

            public static implicit operator MemberAccess<P>(StaticProperty<P> property) => property?.accessor;

            /// <summary>
            /// Gets static property.
            /// </summary>
            /// <param name="propertyName">Name of property.</param>
            /// <param name="nonPublic">True to reflect non-public property.</param>
            /// <returns>Static property; or null, if property doesn't exist.</returns>
            public static StaticProperty<P> Get(string propertyName, bool nonPublic = false)
                => (nonPublic ? NonPublic : Public).GetOrCreate(propertyName);

            /// <summary>
            /// Gets static property.
            /// </summary>
            /// <param name="propertyName">Name of property.</param>
            /// <param name="nonPublic">True to reflect non-public property.</param>
            /// <typeparam name="E">Type of exception to throw if property doesn't exist.</typeparam>
            /// <returns>Static property.</returns>
            public static StaticProperty<P> GetOrThrow<E>(string propertyName, bool nonPublic = false)
                where E : Exception, new()
                => Get(propertyName, nonPublic) ?? throw new E();

            /// <summary>
            /// Gets static property.
            /// </summary>
            /// <param name="propertyName">Name of property.</param>
            /// <param name="exceptionFactory">A factory used to produce exception.</param>
            /// <param name="nonPublic">True to reflect non-public property.</param>
            /// <typeparam name="E">Type of exception to throw if property doesn't exist.</typeparam>
            /// <returns>Static property.</returns>
            public static StaticProperty<P> GetOrThrow<E>(string propertyName, Func<string, E> exceptionFactory, bool nonPublic = false)
                where E : Exception
                => Get(propertyName, nonPublic) ?? throw exceptionFactory(propertyName);

            /// <summary>
            /// Gets static property.
            /// </summary>
            /// <param name="propertyName">Name of property.</param>
            /// <param name="nonPublic">True to reflect non-public property.</param>
            /// <returns>Static property.</returns>
            /// <exception cref="MissingPropertyException">Property doesn't exist.</exception>
            public static StaticProperty<P> GetOrThrow(string propertyName, bool nonPublic = false)
                => GetOrThrow(propertyName, MissingPropertyException.Create<T, P>, nonPublic);
        }

        /// <summary>
        /// Provides typed access to instance property declared in type <typeparamref name="T"/>.
        /// </summary>
		/// <typeparam name="P">Type of property.</typeparam>
        public sealed class InstanceProperty<P> : Property<P>, IProperty<T, P>
        {
            private sealed class Cache : MemberCache<PropertyInfo, InstanceProperty<P>>
            {
                private readonly BindingFlags flags;

                internal Cache(BindingFlags flags) => this.flags = flags;

                private protected override InstanceProperty<P> Create(string propertyName)
                {
                    var property = RuntimeType.GetProperty(propertyName, flags);
                    return property == null ? null : new InstanceProperty<P>(property, flags.HasFlag(BindingFlags.NonPublic));
                }
            }

            private static readonly Cache Public = new Cache(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            private static readonly Cache NonPublic = new Cache(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

            private readonly MemberAccess<T, P> accessor;

            private InstanceProperty(PropertyInfo property, bool nonPublic)
                : base(property)
            {
                var instanceParam = Parameter(RuntimeType.MakeByRefType());
                var valueParam = Parameter(property.PropertyType.MakeByRefType());
                var actionParam = Parameter(typeof(MemberAction));

                var getter = property.GetGetMethod(nonPublic);
                var setter = property.GetSetMethod(nonPublic);

                if (getter is null) //write-only
                    accessor = Lambda<MemberAccess<T, P>>(MemberAccess.GetOrSetValue(actionParam, null, Call(instanceParam, setter, valueParam)),
                        instanceParam,
                        valueParam,
                        actionParam).Compile();
                else if (setter is null) //read-only
                    accessor = Lambda<MemberAccess<T, P>>(MemberAccess.GetOrSetValue(actionParam, Assign(valueParam, Call(instanceParam, getter)), null),
                    instanceParam,
                        valueParam,
                        actionParam).Compile();
                else //read-write
                    accessor = Lambda<MemberAccess<T, P>>(MemberAccess.GetOrSetValue(actionParam, Assign(valueParam, Call(instanceParam, getter)), Call(instanceParam, setter, valueParam)),
                        instanceParam,
                        valueParam,
                        actionParam).Compile();
            }

            // public new Method<MemberAccess.Getter<T, P>> GetGetMethod(bool nonPublic)
            // {
            //     var getter = base.GetGetMethod(nonPublic);
            //     return getter == null ? null : InstanceMethod<MemberAccess.Getter<T, P>>.Get(getter.Name, nonPublic);
            // }

            // public new Method<MemberAccess.Getter<T, P>> GetMethod
            // {
            //     get
            //     {
            //         var getter = base.GetMethod;
            //         return getter == null ? null : InstanceMethod<MemberAccess.Getter<T, P>>.Get(getter.Name, !getter.IsPublic);
            //     }
            // }
            // public new Method<MemberAccess.Setter<T, P>> SetMethod
            // {
            //     get
            //     {
            //         var setter = base.SetMethod;
            //         return setter == null ? null : InstanceMethod<MemberAccess.Setter<T, P>>.Get(setter.Name, !setter.IsPublic);
            //     }
            // }

            // public new Method<MemberAccess.Setter<T, P>> GetSetMethod(bool nonPublic)
            // {
            //     var setter = base.GetSetMethod(nonPublic);
            //     return setter == null ? null : InstanceMethod<MemberAccess.Setter<T, P>>.Get(setter.Name, nonPublic);
            // }

            public static implicit operator MemberAccess<T, P>(InstanceProperty<P> property) => property?.accessor;

            /// <summary>
            /// Gets or sets property value.
            /// </summary>
            /// <param name="owner">Property instance.</param>
            /// <returns>Property value.</returns>
            public P this[in T owner]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => accessor.GetValue(in owner);
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set => accessor.SetValue(in owner, value);
            }

            /// <summary>
            /// Gets instance property.
            /// </summary>
            /// <param name="propertyName">Name of property.</param>
            /// <param name="nonPublic">True to reflect non-public property.</param>
            /// <returns>Static instance; or null, if property doesn't exist.</returns>
            public static InstanceProperty<P> Get(string propertyName, bool nonPublic = false)
                => (nonPublic ? NonPublic : Public).GetOrCreate(propertyName);

            /// <summary>
            /// Gets instance property.
            /// </summary>
            /// <param name="propertyName">Name of property.</param>
            /// <param name="nonPublic">True to reflect non-public property.</param>
            /// <typeparam name="E">Type of exception to throw if property doesn't exist.</typeparam>
            /// <returns>Instance property.</returns>
            public static InstanceProperty<P> GetOrThrow<E>(string propertyName, bool nonPublic = false)
                where E : Exception, new()
                => Get(propertyName, nonPublic) ?? throw new E();

            /// <summary>
            /// Gets instance property.
            /// </summary>
            /// <param name="propertyName">Name of property.</param>
            /// <param name="exceptionFactory">A factory used to produce exception.</param>
            /// <param name="nonPublic">True to reflect non-public property.</param>
            /// <typeparam name="E">Type of exception to throw if property doesn't exist.</typeparam>
            /// <returns>Instance property.</returns>
            public static InstanceProperty<P> GetOrThrow<E>(string propertyName, Func<string, E> exceptionFactory, bool nonPublic = false)
                where E : Exception
                => Get(propertyName, nonPublic) ?? throw exceptionFactory(propertyName);

            /// <summary>
            /// Gets instance property.
            /// </summary>
            /// <param name="propertyName">Name of property.</param>
            /// <param name="nonPublic">True to reflect non-public property.</param>
            /// <returns>Static property.</returns>
            /// <exception cref="MissingPropertyException">Property doesn't exist.</exception>
            public static InstanceProperty<P> GetOrThrow(string propertyName, bool nonPublic = false)
                => GetOrThrow(propertyName, MissingPropertyException.Create<T, P>, nonPublic);
        }
    }
}