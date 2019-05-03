namespace DotNext
{
    /// <summary>
    /// Represents item indexer delegate which can be used
    /// to read and modify collection element during iteration.
    /// </summary>
    /// <typeparam name="I">Type of item index.</typeparam>
    /// <typeparam name="V">Type of collection element.</typeparam>
    /// <param name="index">Element index.</param>
    /// <param name="element">Mutable managed pointer to array element.</param>
    public delegate void ItemAction<in I, V>(I index, ref V element);
}
