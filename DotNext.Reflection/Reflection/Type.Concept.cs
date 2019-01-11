namespace DotNext.Reflection
{
    public static partial class Type<T>
    {
        private static class ConceptHolder<C>
            where C: IConcept<T>, new()
        {
            internal static readonly C Value = new C();
        }

        /// <summary>
        /// Applies a concept to type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="C">Type of concept.</typeparam>
        /// <returns>Concept instance.</returns>
        public static C Concept<C>()
            where C: IConcept<T>, new()
            => ConceptHolder<C>.Value;
    }
}