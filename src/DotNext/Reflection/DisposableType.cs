using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;

namespace DotNext.Reflection
{
    /// <summary>
    /// Provides specialized reflection methods for
    /// types implementing dispose pattern.
    /// </summary>
    public static class DisposableType
    {
        private const BindingFlags PublicInstanceMethod = BindingFlags.Instance | BindingFlags.Public;

        private static MethodInfo? GetDisposeMethod(Type type, Type disposableType, string disposeMethodName, Type returnType)
        {
            if (disposableType.IsAssignableFrom(type))
                return disposableType.GetMethod(disposeMethodName, Type.EmptyTypes);

            var candidate = type.GetMethod(disposeMethodName, PublicInstanceMethod, null, Type.EmptyTypes, null);
            return candidate is null || candidate.IsGenericMethod || candidate.ReturnType != returnType ? null : candidate;
        }

        /// <summary>
        /// Gets <c>Dispose</c> method which implements dispose pattern.
        /// </summary>
        /// <remarks>
        /// This method checks whether the type implements <see cref="IDisposable"/>.
        /// If it is then return <see cref="IDisposable.Dispose"/> method. Otherwise,
        /// return public instance method with name Dispose.
        /// </remarks>
        /// <param name="type">The type to inspect.</param>
        /// <returns>Dispose method; or <see langword="null"/>, if this method doesn't exist.</returns>
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
        public static MethodInfo? GetDisposeMethod(this Type type)
            => GetDisposeMethod(type, typeof(IDisposable), nameof(IDisposable.Dispose), typeof(void));

        /// <summary>
        /// Gets <c>DisposeAsync</c> method implementing async dispose pattern.
        /// </summary>
        /// <param name="type">The type to inspect.</param>
        /// <returns>Dispose method; or <see langword="null"/>, if this method doesn't exist.</returns>
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
        public static MethodInfo? GetDisposeAsyncMethod(this Type type)
            => GetDisposeMethod(type, typeof(IAsyncDisposable), nameof(IAsyncDisposable.DisposeAsync), typeof(ValueTask));
    }
}