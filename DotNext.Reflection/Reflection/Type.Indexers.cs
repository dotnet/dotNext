namespace DotNext.Reflection
{
    public static partial class Type<T>
    {
        public static class Indexer<A, V>
            where A: struct
        {
            public static Reflection.Indexer<A, V> GetStatic(string propertyName, bool nonPublic = false)
                => Reflection.Indexer<A, V>.Reflect<T>(propertyName, nonPublic);
        }
    }
}
