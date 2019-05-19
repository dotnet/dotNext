using System;
using System.Reflection;

namespace DotNext.Reflection
{
    /// <summary>
    /// Provides specialized reflection methods for
    /// types implementing dispose pattern.
    /// </summary>
    public static class DisposableType
    {
        /// <summary>
        /// Gets Dispose method which implements dispose pattern.
        /// </summary>
        /// <remarks>
        /// This method checks whether the type implements <see cref="IDisposable"/>.
        /// If it is then return <see cref="IDisposable.Dispose"/> method. Otherwise,
        /// return public instance method with name Dispose.
        /// </remarks>
        /// <param name="type">The type to inspect.</param>
        /// <returns>Dispose method; or <see langword="null"/>, if this method doesn't exist.</returns>
        public static MethodInfo GetDisposeMethod(this Type type)
        {
            const string DisposeMethodName = nameof(IDisposable.Dispose);
            const BindingFlags PublicInstanceMethod = BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy;
            return typeof(IDisposable).IsAssignableFrom(type) ?
                typeof(IDisposable).GetMethod(DisposeMethodName) :
                type.GetMethod(DisposeMethodName, PublicInstanceMethod, Type.DefaultBinder, Array.Empty<Type>(), Array.Empty<ParameterModifier>());
        }
    }
}