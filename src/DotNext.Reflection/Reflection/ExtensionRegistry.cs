using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace DotNext.Reflection
{
    using Seq = Collections.Generic.Sequence;

    /// <summary>
    /// Represents registry of extension methods that can be registered
    /// for the specified type and be available using strongly typed reflection via <see cref="Type{T}"/>.
    /// </summary>
    public sealed class ExtensionRegistry : ConcurrentBag<MethodInfo>
    {
        private static readonly UserDataSlot<ExtensionRegistry> InstanceMethods = UserDataSlot<ExtensionRegistry>.Allocate();
        private static readonly UserDataSlot<ExtensionRegistry> StaticMethods = UserDataSlot<ExtensionRegistry>.Allocate();

        private ExtensionRegistry()
        {
        }

        private static ExtensionRegistry Create() => new ExtensionRegistry();

        private static IEnumerable<MethodInfo> GetMethods(IEnumerable<Type> types, UserDataSlot<ExtensionRegistry> registrySlot)
        {
            foreach (var type in types)
            {
                foreach (var method in type.GetUserData().Get(registrySlot) ?? Enumerable.Empty<MethodInfo>())
                    yield return method;
            }
        }

        private static IEnumerable<MethodInfo> GetStaticMethods(Type target)
            => GetMethods(Seq.Singleton(target.NonRefType()), StaticMethods);

        private static IEnumerable<MethodInfo> GetInstanceMethods(Type target)
        {
            IEnumerable<Type> types;
            if (target.IsValueType)
            {
                types = Seq.Singleton(target);
            }
            else if (target.IsByRef)
            {
                var underlyingType = target.GetElementType();
                Debug.Assert(underlyingType is not null);
                types = Seq.Singleton(underlyingType);
            }
            else
            {
                types = target.GetBaseTypes(includeTopLevel: true, includeInterfaces: true);
            }

            return GetMethods(types, InstanceMethods);
        }

        internal static IEnumerable<MethodInfo> GetMethods(Type target, MethodLookup lookup) => lookup switch
        {
            MethodLookup.Static => GetStaticMethods(target),
            MethodLookup.Instance => GetInstanceMethods(target),
            _ => Enumerable.Empty<MethodInfo>(),
        };

        private static ExtensionRegistry GetOrCreateRegistry(Type target, MethodLookup lookup)
        {
            var registrySlot = lookup switch
            {
                MethodLookup.Instance => InstanceMethods,
                MethodLookup.Static => StaticMethods,
                _ => throw new ArgumentOutOfRangeException(nameof(lookup)),
            };
            return target.GetUserData().GetOrSet(registrySlot, new ValueFunc<ExtensionRegistry>(Create));
        }

        /// <summary>
        /// Registers static method for the specified type in ad-hoc manner so
        /// it will be available using <see cref="Type{T}.Method.Get{D}(string, MethodLookup, bool)"/> and related methods.
        /// </summary>
        /// <typeparam name="T">The type to be extended with static method.</typeparam>
        /// <param name="method">The static method implementation.</param>
        public static void RegisterStatic<T>(MethodInfo method) => GetOrCreateRegistry(typeof(T), MethodLookup.Static).Add(method);

        /// <summary>
        /// Registers static method for the specified type in ad-hoc manner so
        /// it will be available using <see cref="Type{T}.Method.Get{D}(string, MethodLookup, bool)"/> and related methods.
        /// </summary>
        /// <typeparam name="T">The type to be extended with static method.</typeparam>
        /// <typeparam name="TExtension">The type of the delegate.</typeparam>
        /// <param name="delegate">The delegate instance representing extension method.</param>
        public static void RegisterStatic<T, TExtension>(TExtension @delegate)
            where TExtension : Delegate
            => RegisterStatic<T>(@delegate.Method);

        /// <summary>
        /// Registers extension method as instance method which will be included into strongly typed
        /// reflection lookup performed by <see cref="Type{T}.Method.Get{D}(string, MethodLookup, bool)"/> and related methods.
        /// </summary>
        /// <param name="method">Static method to register. Cannot be <see langword="null"/>.</param>
        public static void RegisterInstance(MethodInfo method)
        {
            var thisParam = method.GetParameterTypes().FirstOrDefault();
            if (!method.IsStatic || thisParam is null)
                throw new ArgumentException(ExceptionMessages.ExtensionMethodExpected(method), nameof(method));
            GetOrCreateRegistry(thisParam.NonRefType(), MethodLookup.Instance).Add(method);
        }

        /// <summary>
        /// Registers extension method which will be included into strongly typed
        /// reflection lookup performed by <see cref="Reflector.Unreflect{D}(MethodInfo)"/>
        /// or <see cref="Type{T}.Method.Get{D}(string, MethodLookup, bool)"/> methods.
        /// </summary>
        /// <typeparam name="TExtension">The type of the delegate.</typeparam>
        /// <param name="delegate">The delegate instance representing extension method.</param>
        public static void RegisterInstance<TExtension>(TExtension @delegate)
            where TExtension : Delegate
            => RegisterInstance(@delegate.Method);
    }
}
