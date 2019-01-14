using System.Reflection;

namespace DotNext.Reflection
{
    public static partial class Type<T>
    {
        /// <summary>
        /// Provides access to indexer property declared in type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="A">A structure representing parameters of indexer.</typeparam>
	    /// <typeparam name="V">Property value.</typeparam>
        public static class Indexer<A, V>
            where A: struct
        {
            private sealed class InstanceProperties : MemberCache<PropertyInfo, Indexer<T, A, V>>
			{
				internal static readonly InstanceProperties Public = new InstanceProperties(false);
				internal static readonly InstanceProperties NonPublic = new InstanceProperties(true);

				private readonly bool nonPublic;
				private InstanceProperties(bool nonPublic) => this.nonPublic = nonPublic;

				private protected override Indexer<T, A, V> Create(string propertyName)
					=> Indexer<T, A, V>.Reflect(propertyName, nonPublic);
			}

            private sealed class StaticProperties : MemberCache<PropertyInfo, Reflection.Indexer<A, V>>
			{
				internal static readonly StaticProperties Public = new StaticProperties(false);
				internal static readonly StaticProperties NonPublic = new StaticProperties(true);

				private readonly bool nonPublic;
				private StaticProperties(bool nonPublic) => this.nonPublic = nonPublic;

				private protected override Reflection.Indexer<A, V> Create(string propertyName)
					=> Reflection.Indexer<A, V>.Reflect<T>(propertyName, nonPublic);
			}

            public static Reflection.Indexer<A, V> GetStatic(string propertyName, bool nonPublic = false)
                => (nonPublic ? StaticProperties.NonPublic : StaticProperties.Public).GetOrCreate(propertyName);
            
            public static Indexer<T, A, V> Get(string propertyName = "Item", bool nonPublic = false)
                => (nonPublic ? InstanceProperties.NonPublic : InstanceProperties.Public).GetOrCreate(propertyName);
        }
    }
}
