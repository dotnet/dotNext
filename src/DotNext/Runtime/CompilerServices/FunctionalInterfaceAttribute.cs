namespace DotNext.Runtime.CompilerServices;

/// <summary>
/// Marks interface as functional interface which has the only one abstract method
/// without default implementation.
/// </summary>
[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
public sealed class FunctionalInterfaceAttribute : Attribute
{
}