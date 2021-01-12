namespace DotNext
{
    /// <summary>
    /// Represents delegate that accepts arbitrary value by reference and support
    /// return value.
    /// </summary>
    /// <typeparam name="T">The type of the object to be passed by reference into the action.</typeparam>
    /// <typeparam name="TArgs">The type of the arguments to be passed into the action.</typeparam>
    /// <typeparam name="TResult">The type of the return value of the method that this delegate encapsulates.</typeparam>
    /// <param name="reference">The object passed by reference.</param>
    /// <param name="args">The action arguments.</param>
    /// <returns>The return value of the method that this delegate encapsulates.</returns>
    public delegate TResult RefFunc<T, in TArgs, out TResult>(ref T reference, TArgs args);
}
