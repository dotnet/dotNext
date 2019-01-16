using System;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace DotNext.Collections.Generic
{
    public readonly struct ReadOnlyCollectionView<T>: IReadOnlyCollection<T>, IEquatable<ReadOnlyCollectionView<T>>
    {
        private readonly ICollection<T> source;

        public ReadOnlyCollectionView(ICollection<T> collection)
            => source = collection ?? throw new ArgumentNullException(nameof(collection));

        public int Count => source.Count;

        public IEnumerator<T> GetEnumerator() => source.GetEnumerator();
        
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool Equals(ReadOnlyCollectionView<T> other) => ReferenceEquals(source, other.source);

        public override int GetHashCode() => RuntimeHelpers.GetHashCode(source);

		public override bool Equals(object other)
			=> other is ReadOnlyCollectionView<T> view ? Equals(view) : Equals(source, other);

		public static bool operator ==(ReadOnlyCollectionView<T> first, ReadOnlyCollectionView<T> second)
			=> first.Equals(second);

		public static bool operator !=(ReadOnlyCollectionView<T> first, ReadOnlyCollectionView<T> second)
			=> !first.Equals(second);
    }
}