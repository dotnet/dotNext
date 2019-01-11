using System;
using Xunit;

namespace DotNext
{
	public sealed class StringsTests: Assert
	{
		[Fact]
		public void IfNullOrEmptyTest()
		{
			Equal("a", "".IfNullOrEmpty("a"));
			Equal("a", default(string).IfNullOrEmpty("a"));
			Equal("b", "b".IfNullOrEmpty("a"));
		}
	}
}
