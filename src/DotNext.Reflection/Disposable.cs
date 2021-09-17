using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using static System.Linq.Expressions.Expression;

namespace DotNext;

using Reflection;
using ConceptAttribute = Runtime.CompilerServices.ConceptAttribute;

/// <summary>
/// Represents dispose pattern as a concept.
/// </summary>
/// <remarks>
/// This concept provides access to Dispose method of type <typeparamref name="T"/> even
/// if it doesn't implement <see cref="IDisposable"/> interface directly.
/// </remarks>
/// <typeparam name="T">A type which implements dispose pattern.</typeparam>
/// <seealso href="https://docs.microsoft.com/en-us/dotnet/standard/garbage-collection/implementing-dispose">Implementing Dispose method</seealso>
[Concept]
public static class Disposable<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] T>
{
    private delegate void DisposeMethod(in T instance);

    private static readonly DisposeMethod DisposeMethodImpl;

    static Disposable()
    {
        var disposeMethod = typeof(T).GetDisposeMethod();
        if (disposeMethod?.DeclaringType is not null && disposeMethod.ReturnType == typeof(void))
        {
            if (disposeMethod.DeclaringType.IsValueType)
            {
                Disposable<T>.DisposeMethodImpl = disposeMethod.CreateDelegate<DisposeMethod>();
            }
            else
            {
                var instance = Parameter(typeof(T).MakeByRefType(), "this");
                Disposable<T>.DisposeMethodImpl = Lambda<DisposeMethod>(Call(instance, disposeMethod), instance).Compile();
            }
        }
        else
        {
            throw new MissingMethodException(typeof(T), nameof(IDisposable.Dispose), typeof(void), Array.Empty<Type>());
        }
    }

    /// <summary>
    /// Disposes specified object.
    /// </summary>
    /// <param name="obj">An object to dispose passed by reference.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Dispose(in T obj) => DisposeMethodImpl(in obj);
}