using System;
using Xunit;

namespace MissingPieces
{
	public sealed class MemoryTests: Assert
	{
		[Fact]
		public void BitwiseEqualityTest2()
		{
			var value1 = Guid.NewGuid();
			var value2 = value1;
			True(value1.BitwiseEquals(value2));
			value2 = default;
			False(value1.BitwiseEquals(value2));
		}

		[Fact]
		public void DefaultTests()
		{
			var value = default(Guid);
            True(value.IsDefault());
            value = Guid.NewGuid();
            False(value.IsDefault());
		}
	}
}
