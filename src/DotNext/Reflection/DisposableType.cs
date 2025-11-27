using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace DotNext.Reflection;

/// <summary>
/// Provides specialized reflection methods for
/// types implementing dispose pattern.
/// </summary>
public static class DisposableType
{
    private const BindingFlags PublicInstanceMethod = BindingFlags.Instance | BindingFlags.Public;

    private static MethodInfo? GetDisposeMethod([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type type, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type disposableType, string disposeMethodName, Type returnType)
    {
        if (disposableType.IsAssignableFrom(type))
            return disposableType.GetMethod(disposeMethodName, []);

        var candidate = type.GetMethod(disposeMethodName, PublicInstanceMethod, binder: null, [], modifiers: null);
        return candidate is null || candidate.IsGenericMethod || candidate.ReturnType != returnType ? null : candidate;
    }

    /// <summary>
    /// Extends <see cref="Type"/> type.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    extension([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type type)
    {
        /// <summary>
        /// Gets <c>Dispose</c> method which implements dispose pattern.
        /// </summary>
        /// <remarks>
        /// This method checks whether the type implements <see cref="IDisposable"/>.
        /// If it is then return <see cref="IDisposable.Dispose"/> method. Otherwise,
        /// return public instance method with name Dispose.
        /// </remarks>
        /// <value>Dispose method; or <see langword="null"/>, if this method doesn't exist.</value>
        public MethodInfo? DisposeMethod
            => GetDisposeMethod(type, typeof(IDisposable), nameof(IDisposable.Dispose), typeof(void));

        /// <summary>
        /// Gets <c>DisposeAsync</c> method implementing async dispose pattern.
        /// </summary>
        /// <value>Dispose method; or <see langword="null"/>, if this method doesn't exist.</value>
        public MethodInfo? DisposeAsyncMethod
            => GetDisposeMethod(type, typeof(IAsyncDisposable), nameof(IAsyncDisposable.DisposeAsync), typeof(ValueTask));
    }
}