using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace DotNext.Reflection;

/// <summary>
/// Various extension methods for method reflection.
/// </summary>
public static class MethodExtensions
{
    /// <summary>
    /// Returns method parameter types respecting order of parameters.
    /// </summary>
    /// <param name="method">The method to reflect.</param>
    /// <returns>The array of parameter types.</returns>
    [return: NotNullIfNotNull("method")]
    public static Type[]? GetParameterTypes(this MethodBase? method)
        => method is null ? null : Array.ConvertAll(method.GetParameters(), static p => p.ParameterType);

    /// <summary>
    /// Determines whether the method parameters have the same set of types as in given array of types.
    /// </summary>
    /// <param name="method">The method to check.</param>
    /// <param name="parameters">The expected parameter types.</param>
    /// <returns><see langword="true"/>, if the method parameters have the same set of types as types passed as array; otherwise, <see langword="false"/>.</returns>
    public static bool SignatureEquals(this MethodBase method, Type[] parameters)
    {
        var firstParams = method.GetParameterTypes();
        if (firstParams.LongLength != parameters.LongLength)
            return false;

        for (long i = 0; i < firstParams.LongLength; i++)
        {
            if (firstParams[i] != parameters[i])
                return false;
        }

        return true;
    }

    /// <summary>
    /// Determines whether formal parameters of both methods are equal by type.
    /// </summary>
    /// <param name="method">The first method to compare.</param>
    /// <param name="other">The second method to compare.</param>
    /// <param name="respectCallingConvention"><see langword="true"/> to check calling convention; <see langword="false"/> to ignore calling convention.</param>
    /// <returns><see langword="true"/>, if both methods have the same number of formal parameters and parameters are equal by type; otherwise, <see langword="false"/>.</returns>
    public static bool SignatureEquals(this MethodBase method, MethodBase other, bool respectCallingConvention = false)
        => (!respectCallingConvention || method.CallingConvention == other.CallingConvention) && method.SignatureEquals(other.GetParameterTypes());

    /// <summary>
    /// Determines whether formal parameters of both methods are equal by type
    /// and return types are also equal.
    /// </summary>
    /// <param name="method">The first method to compare.</param>
    /// <param name="other">The second method to compare.</param>
    /// <param name="respectCallingConvention"><see langword="true"/> to check calling convention; <see langword="false"/> to ignore calling convention.</param>
    /// <returns><see langword="true"/>, if both methods have the same number of formal parameters, parameters are equal by type and return types are equal; otherwise, <see langword="false"/>.</returns>
    public static bool SignatureEquals(this MethodInfo method, MethodInfo other, bool respectCallingConvention = false)
        => SignatureEquals(method.As<MethodBase>(), other, respectCallingConvention) && method.ReturnType == other.ReturnType;

    /// <summary>
    /// Invokes the method or constructor represented by the current instance, using the specified arguments.
    /// </summary>
    /// <remarks>
    /// This method never throws an exception.
    /// Examine <see cref="Result{T}.IsSuccessful"/> or <see cref="Result{T}.Error"/> instead.
    /// </remarks>
    /// <param name="method">The method or constructor to invoke.</param>
    /// <param name="obj">
    /// The object on which to invoke the method or constructor. If a method is static,
    /// this argument is ignored. If a constructor is static, this argument must be <see langword="null"/>
    /// or an instance of the class that defines the constructor.
    /// </param>
    /// <param name="args">An argument list for the invoked method or constructor.</param>
    /// <returns>An object containing the return value of the invoked method, or <see langword="null"/> in the case of a constructor.</returns>
    public static Result<object?> TryInvoke(this MethodBase method, object? obj, params object?[] args)
    {
        Result<object?> result;
        try
        {
            result = method.Invoke(obj, args);
        }
        catch (Exception e)
        {
            result = new(e);
        }

        return result;
    }
}