namespace DotNext.Runtime.CompilerServices;

/// <summary>
/// Represents root interface for all functional interfaces.
/// </summary>
/// <typeparam name="TDelegate">The type of the delegate representing signature of the functional interface.</typeparam>
public interface IFunctional<out TDelegate>
    where TDelegate : Delegate
{
    /// <summary>
    /// Converts functional object to the delegate.
    /// </summary>
    /// <returns>The delegate representing this functional object.</returns>
    TDelegate ToDelegate();
}