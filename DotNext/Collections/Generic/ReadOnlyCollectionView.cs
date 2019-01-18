using System;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;

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

    public readonly struct ReadOnlyCollectionView<I, O>: IReadOnlyCollection<O>, IEquatable<ReadOnlyCollectionView<I, O>>
    {
        private readonly IReadOnlyCollection<I> source;
        private readonly Converter<I, O> mapper;

        public ReadOnlyCollectionView(IReadOnlyCollection<I> collection, Converter<I, O> mapper)
        {
            source = collection ?? throw new ArgumentNullException(nameof(collection));
			this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public int Count => source.Count;

        public IEnumerator<O> GetEnumerator() => source.Select(mapper.ConvertDelegate<Func<I, O>>()).GetEnumerator();
        
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool Equals(ReadOnlyCollectionView<I, O> other) => ReferenceEquals(source, other.source) && Equals(mapper, other.mapper);

        public override int GetHashCode() => source is null || mapper is null ? 0 : RuntimeHelpers.GetHashCode(source) ^ mapper.GetHashCode();

		public override bool Equals(object other)
			=> other is ReadOnlyCollectionView<I, O> view ? Equals(view) : Equals(source, other);

		public static bool operator ==(ReadOnlyCollectionView<I, O> first, ReadOnlyCollectionView<I, O> second)
			=> first.Equals(second);

		public static bool operator !=(ReadOnlyCollectionView<I, O> first, ReadOnlyCollectionView<I, O> second)
			=> !first.Equals(second);
    }
}