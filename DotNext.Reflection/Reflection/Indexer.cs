using System;
using System.Reflection;

namespace DotNext.Reflection
{
    public abstract class IndexerBase<A>: PropertyInfo, IProperty, IEquatable<IndexerBase<A>>, IEquatable<PropertyInfo>
        where A: struct
    {
        private readonly PropertyInfo property;

        private protected IndexerBase(PropertyInfo property) => this.property = property;

        PropertyInfo IMember<PropertyInfo>.RuntimeMember => property;

        public bool Equals(PropertyInfo other) => property == other;

        public bool Equals(IndexerBase<A> other)
            => other != null &&
                GetType() == other.GetType() &&
                property == other.property;
    }
}