namespace DotNext
{
    /// <summary>
    /// Represents action that accepts arbitrary value by reference.
    /// </summary>
    /// <typeparam name="T">The type of the object to be passed by reference into the action.</typeparam>
    /// <typeparam name="TArgs">The type of the arguments to be passed into the action.</typeparam>
    /// <param name="reference">The object passed by reference.</param>
    /// <param name="args">The action arguments.</param>
    public delegate void RefAction<T, in TArgs>(ref T reference, TArgs args);
}
