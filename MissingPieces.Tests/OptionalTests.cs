using System;
using Xunit;

namespace MissingPieces
{
	public sealed class OptionalTest : Assert
	{
		private struct HasContentMutableStruct : IOptional
		{
			internal int counter;

			bool IOptional.IsPresent
			{
				get
				{
					counter += 1;
					return false;
				}
			}
		}

		private readonly struct HasContentStruct : IOptional
		{
			internal HasContentStruct(bool hasContent)
				=> IsPresent = hasContent;

			public bool IsPresent { get; }
		}

		private sealed class HasContentClass : IOptional
		{
			public HasContentClass(bool hasContent)
				=> IsPresent = hasContent;

			public bool IsPresent { get; }
		}

		[Fact]
		public void NullableTest()
		{
			False(Optional<int?>.HasValue(null));
			True(Optional<long?>.HasValue(10L));
			False(Optional<HasContentStruct?>.HasValue(null));
			False(Optional<HasContentStruct?>.HasValue(new HasContentStruct(false)));
			True(Optional<HasContentStruct?>.HasValue(new HasContentStruct(true)));
		}

		/// <summary>
		/// This test checks whether the
		/// optional test doesn't cause boxing.
		/// </summary>
		[Fact]
		public void MutableStructTest()
		{
			var value = new HasContentMutableStruct();
			Optional<HasContentMutableStruct>.HasValue(value);
			Optional<HasContentMutableStruct>.HasValue(value);
			Equal(2, value.counter);
		}

		[Fact]
		public void OptionalTypeTest()
		{
			var intOptional = new int?(10).ToOptional();
			True(intOptional.IsPresent);
			Equal(10, (int)intOptional);
			Equal(10, intOptional.Or(20));
			Equal(10, intOptional.Value);
			True(Nullable.Equals(10, intOptional.GetOrNull()));
			Equal(typeof(int), Optional.GetUnderlyingType(intOptional.GetType()));

			intOptional = default(int?).ToOptional();
			False(intOptional.IsPresent);
			Equal(20, intOptional.Or(20));
			True(Nullable.Equals(null, intOptional.GetOrNull()));
			Equal(30, intOptional.Coalesce(new int?(30).ToOptional()).Value);
			Equal(40, (intOptional | new int?(40).ToOptional()).Value);
			Throws<InvalidOperationException>(() => intOptional.Value);

			Optional<string> strOptional = null;
			False(strOptional.IsPresent);
			Equal("Hello, world", strOptional.Or("Hello, world"));
			Throws<InvalidOperationException>(() => strOptional.Value);
			Equal(typeof(string), Optional.GetUnderlyingType(strOptional.GetType()));
		}

		[Fact]
		public void StructTest()
		{
			False(Optional<ValueTuple>.HasValue(default));
			True(Optional<long>.HasValue(default));
			False(Optional<HasContentStruct>.HasValue(default));
			False(Optional<HasContentStruct>.HasValue(new HasContentStruct(false)));
			True(Optional<HasContentStruct>.HasValue(new HasContentStruct(true)));
			True(Optional<Base64FormattingOptions>.HasValue(Base64FormattingOptions.InsertLineBreaks));
		}

		[Fact]
		public void ClassTest()
		{
			True(Optional<Optional<string>>.HasValue((Optional<string>)""));
			False(Optional<string>.HasValue(default));
			True(Optional<string>.HasValue(""));
			False(Optional<HasContentClass>.HasValue(default));
			False(Optional<HasContentClass>.HasValue(new HasContentClass(false)));
			True(Optional<HasContentClass>.HasValue(new HasContentClass(true)));
			False(Optional<Delegate>.HasValue(default));
			True(Optional<EventHandler>.HasValue((sender, args) => { }));
		}
	}
}
