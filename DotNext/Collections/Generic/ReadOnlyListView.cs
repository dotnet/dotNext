using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DotNext.Collections.Generic
{
    public readonly struct ReadOnlyListView<T>: IReadOnlyList<T>
    {
        private readonly IList<T> source;

        public ReadOnlyListView(IList<T> list)
            => source = list ?? throw new ArgumentNullException(nameof(list));

        public int Count => source.Count;

        public T this[int index] => source[index];

        public IEnumerator<T> GetEnumerator() => source.GetEnumerator();
        
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool Equals(ReadOnlyListView<T> other) => ReferenceEquals(source, other.source);

        public override int GetHashCode() => RuntimeHelpers.GetHashCode(source);

		public override bool Equals(object other)
			=> other is ReadOnlyCollectionView<T> view ? Equals(view) : Equals(source, other);

		public static bool operator ==(ReadOnlyListView<T> first, ReadOnlyListView<T> second)
			=> first.Equals(second);

		public static bool operator !=(ReadOnlyListView<T> first, ReadOnlyListView<T> second)
			=> !first.Equals(second);
    }

    public readonly struct ReadOnlyListView<I, O>: IReadOnlyCollection<O>, IEquatable<ReadOnlyListView<I, O>>
    {
        private readonly IReadOnlyList<I> source;
        private readonly Converter<I, O> mapper;

        public ReadOnlyListView(IReadOnlyList<I> list, Converter<I, O> mapper)
        {
            source = list ?? throw new ArgumentNullException(nameof(list));
			this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public O this[int index] => mapper(source[index]);

        public int Count => source.Count;

        public IEnumerator<O> GetEnumerator() => source.Select(mapper.AsFunc()).GetEnumerator();
        
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool Equals(ReadOnlyListView<I, O> other) => ReferenceEquals(source, other.source) && Equals(mapper, other.mapper);

        public override int GetHashCode() => source is null || mapper is null ? 0 : RuntimeHelpers.GetHashCode(source) ^ mapper.GetHashCode();

		public override bool Equals(object other)
			=> other is ReadOnlyListView<I, O> view ? Equals(view) : Equals(source, other);

		public static bool operator ==(ReadOnlyListView<I, O> first, ReadOnlyListView<I, O> second)
			=> first.Equals(second);

		public static bool operator !=(ReadOnlyListView<I, O> first, ReadOnlyListView<I, O> second)
			=> !first.Equals(second);
    }
}